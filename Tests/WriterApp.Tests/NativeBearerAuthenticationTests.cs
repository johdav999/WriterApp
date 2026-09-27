using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using WriterApp.Application.Security;
using Xunit;

namespace WriterApp.Tests;

public sealed class NativeBearerAuthenticationTests : IDisposable
{
    private const string Tenant = "11111111-1111-1111-1111-111111111111";
    private const string Audience = "22222222-2222-2222-2222-222222222222";
    private const string Client = "33333333-3333-3333-3333-333333333333";
    private const string UserId = "44444444-4444-4444-4444-444444444444";
    private const string PersistedAdminId = "55555555-5555-5555-5555-555555555555";
    private const string Issuer = "https://tenant.ciamlogin.com/11111111-1111-1111-1111-111111111111/v2.0";
    private readonly RSA _rsa = RSA.Create(2048);
    private RsaSecurityKey Key => new(_rsa) { KeyId = "test-key" };

    private IHost Start(bool enabled = true, bool development = false)
    {
        return new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
        {
            services.AddRouting();
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, EasyAuthAuthenticationHandler>(EasyAuthAuthenticationHandler.SchemeName, _ => { })
                .AddScheme<AuthenticationSchemeOptions, LocalDevAuthenticationHandler>(LocalDevAuthenticationHandler.SchemeName, _ => { });
            services.AddNativeBearerAuthentication(new NativeBearerOptions
            {
                Enabled = enabled, Authority = Issuer, Issuer = Issuer, TenantId = Tenant,
                Audience = Audience, AllowedClientIds = [Client]
            }, development ? LocalDevAuthenticationHandler.SchemeName : EasyAuthAuthenticationHandler.SchemeName);
            services.PostConfigure<JwtBearerOptions>(NativeBearerAuthentication.Scheme, options =>
            {
                options.Configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                options.Configuration.SigningKeys.Add(Key); // Fixed test discovery: no live tenant/network.
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(options.Configuration);
            });
            services.AddSingleton<IAdminAccessResolver, TestAdminResolver>();
            services.AddSingleton<IAuthorizationHandler, AdminOnlyAuthorizationHandler>();
            services.AddAuthorization(options => options.AddPolicy("AdminOnly", policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new AdminOnlyRequirement())));
        }).Configure(app =>
        {
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/api/protected", context => context.Response.WriteAsync(
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "missing")).RequireAuthorization();
                endpoints.MapGet("/api/native/session", () => "ok").RequireAuthorization(NativeBearerAuthentication.Policy);
                endpoints.MapGet("/api/admin", () => "admin").RequireAuthorization("AdminOnly");
                endpoints.MapGet("/web", () => "web").RequireAuthorization();
            });
        })).Start();
    }

    private string Token(string scenario = "valid", bool admin = false)
    {
        var claims = new List<Claim>
        {
            new("ver", "2.0"), new("tid", scenario == "tenant" ? Guid.NewGuid().ToString() : Tenant),
            new("oid", scenario == "persisted-admin" ? PersistedAdminId : UserId), new("sub", "pairwise-subject"), new("azp", scenario == "client" ? Guid.NewGuid().ToString() : Client),
            new("scp", scenario == "scope" ? "other_scope" : "access_as_user"), new("name", "Writer")
        };
        if (scenario == "app-only") claims.RemoveAll(c => c.Type == "scp");
        if (scenario == "no-oid") claims.RemoveAll(c => c.Type == "oid");
        if (scenario == "id-token") claims.RemoveAll(c => c.Type == "scp" || c.Type == "azp");
        if (admin) { claims.Add(new("roles", "Admin")); claims.Add(new("appRole", "Admin")); claims.Add(new(ClaimTypes.Role, "Admin")); claims.Add(new("native:unused-role", "Admin")); }
        var token = new JwtSecurityToken(scenario == "issuer" ? "https://other.example/v2.0" : Issuer,
            scenario == "audience" ? Client : Audience, claims,
            DateTime.UtcNow.AddMinutes(-10), scenario == "expired" ? DateTime.UtcNow.AddMinutes(-2) : DateTime.UtcNow.AddMinutes(5),
            scenario == "algorithm" ? new SigningCredentials(new SymmetricSecurityKey(new byte[32]), SecurityAlgorithms.HmacSha256)
                : new SigningCredentials(Key, SecurityAlgorithms.RsaSha256));
        string signed = new JwtSecurityTokenHandler().WriteToken(token);
        if (scenario == "signature")
        {
            string[] parts = signed.Split('.');
            parts[2] = Base64UrlEncoder.Encode(new byte[256]);
            signed = string.Join('.', parts);
        }
        return signed;
    }

    private static void WebIdentity(HttpClient client, bool admin = false)
    {
        var claims = new List<object> { new { typ = "oid", val = UserId }, new { typ = "name", val = "Web writer" } };
        if (admin) claims.Add(new { typ = ClaimTypes.Role, val = "Admin" });
        client.DefaultRequestHeaders.Add("X-MS-CLIENT-PRINCIPAL", Convert.ToBase64String(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { auth_typ = "externalid", claims }))));
    }

    [Fact]
    public async Task ValidDelegatedTokenCallsProtectedApiWithSameCanonicalUserIdAsWeb()
    {
        using var server = Start(); using var client = server.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        Assert.Equal(UserId, await client.GetStringAsync("/api/protected"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/native/session")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        WebIdentity(client);
        Assert.Equal(UserId, await client.GetStringAsync("/api/protected"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/native/session")).StatusCode);
    }

    [Theory]
    [InlineData("audience")]
    [InlineData("issuer")]
    [InlineData("tenant")]
    [InlineData("client")]
    [InlineData("scope")]
    [InlineData("expired")]
    [InlineData("signature")]
    [InlineData("app-only")]
    [InlineData("id-token")]
    [InlineData("no-oid")]
    [InlineData("algorithm")]
    public async Task InvalidTokensNeverFallBackToValidWebSession(string scenario)
    {
        using var server = Start(); using var client = server.GetTestClient();
        WebIdentity(client, admin: true);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(scenario));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/protected")).StatusCode);
    }

    [Theory]
    [InlineData("Bearer invalid")]
    [InlineData("Bearer")]
    [InlineData("Basic abc")]
    public async Task MalformedAuthorizationCannotFallBackToDevelopmentAdmin(string header)
    {
        using var server = Start(development: true); using var client = server.GetTestClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", header);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/protected")).StatusCode);
        client.DefaultRequestHeaders.Remove("Authorization");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/protected")).StatusCode);
    }

    [Fact]
    public async Task NativeApiRolesCannotGrantAdminButExistingWebAdminPolicyStillWorks()
    {
        using var server = Start(); using var client = server.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(admin: true));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token("persisted-admin"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        WebIdentity(client, admin: true);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin")).StatusCode);
    }

    [Fact]
    public async Task DisabledNativeAuthAndNonApiRoutesRejectNativeTokens()
    {
        using var disabled = Start(enabled: false); using var client = disabled.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/protected")).StatusCode);
        using var enabled = Start(); using var other = enabled.GetTestClient();
        other.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/web")).StatusCode);
    }

    private sealed class TestAdminResolver : IAdminAccessResolver
    {
        public AdminAccessDiagnosticInfo Describe(ClaimsPrincipal user)
        {
            bool admin = user.IsInRole("Admin") || ExternalIdentityClaims.ResolveStableUserId(user.Claims) == PersistedAdminId;
            return new(new(admin, admin ? AdminAccessSource.Role : AdminAccessSource.None, AdminAccessReason.None),
                admin, false, admin, false, false, true, false);
        }
        public AdminAccessResolution Resolve(ClaimsPrincipal user) => Describe(user).Resolution;
        public AdminAccessResolution ResolveForUserId(string? id) => AdminAccessResolution.None;
        public AdminAccessResolution ResolveForUserId(string? id, ClaimsPrincipal? principal) => principal is null ? AdminAccessResolution.None : Resolve(principal);
        public IReadOnlyDictionary<string, AdminAccessResolution> ResolveForUserIds(IEnumerable<string> ids, ClaimsPrincipal? principal = null) => new Dictionary<string, AdminAccessResolution>();
        public bool HasPersistedRoleAdmin(string? id) => false;
    }
    public void Dispose() => _rsa.Dispose();
}
