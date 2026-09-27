using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace WriterApp.Application.Security;

public sealed class NativeBearerOptions
{
    public bool Enabled { get; set; }
    public string Authority { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Audience { get; set; } = "";
    public string RequiredScope { get; set; } = "access_as_user";
    public string[] AllowedClientIds { get; set; } = [];

    public void Validate()
    {
        if (!Enabled) return;
        if (!IsHttps(Authority) || !IsHttps(Issuer) || !Guid.TryParse(TenantId, out _)
            || !Guid.TryParse(Audience, out _) || string.IsNullOrWhiteSpace(RequiredScope)
            || RequiredScope.Any(char.IsWhiteSpace) || AllowedClientIds.Length == 0
            || AllowedClientIds.Any(id => !Guid.TryParse(id, out _)))
            throw new InvalidOperationException("NativeAuth requires HTTPS authority/issuer, tenant and API client GUIDs, a delegated scope, and allowed native client GUIDs.");
    }
    private static bool IsHttps(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
}

public static class NativeBearerAuthentication
{
    public const string Scheme = "NativeBearer";
    public const string Router = "WebOrNative";
    public const string Policy = "NativeApi";

    // Any explicit Authorization header must fail closed, including malformed/unsupported schemes.
    public static string SelectScheme(HttpContext context, string webScheme) =>
        context.Request.Headers.ContainsKey("Authorization") ? Scheme : webScheme;

    public static IServiceCollection AddNativeBearerAuthentication(this IServiceCollection services,
        NativeBearerOptions settings, string webScheme)
    {
        settings.Validate();
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = Router;
            options.DefaultChallengeScheme = Router;
            options.DefaultForbidScheme = Router;
        }).AddPolicyScheme(Router, Router, options => options.ForwardDefaultSelector = context => SelectScheme(context, webScheme))
          .AddJwtBearer(Scheme, options =>
          {
              options.MapInboundClaims = false;
              options.SaveToken = false;
              options.IncludeErrorDetails = false;
              options.RequireHttpsMetadata = true;
              if (settings.Enabled) options.Authority = settings.Authority;
              options.TokenValidationParameters = new TokenValidationParameters
              {
                  ValidateIssuer = true, ValidIssuer = settings.Issuer,
                  IssuerValidator = (issuer, _, _) => string.Equals(issuer, settings.Issuer, StringComparison.Ordinal)
                      ? issuer : throw new SecurityTokenInvalidIssuerException("Unexpected native token issuer."),
                  ValidateAudience = true, ValidAudience = settings.Audience,
                  ValidateLifetime = true, RequireExpirationTime = true,
                  RequireSignedTokens = true, ValidateIssuerSigningKey = true,
                  ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.FromSeconds(30),
                  NameClaimType = "name", RoleClaimType = "native:unused-role"
              };
              options.Events = new JwtBearerEvents
              {
                  OnMessageReceived = context =>
                  {
                      if (!settings.Enabled || !context.Request.Path.StartsWithSegments("/api"))
                          context.Fail("Native API authentication is unavailable for this request.");
                      return Task.CompletedTask;
                  },
                  OnTokenValidated = context =>
                  {
                      ClaimsPrincipal user = context.Principal!;
                      if (user.FindFirst("ver")?.Value != "2.0"
                          || !string.Equals(user.FindFirst("tid")?.Value, settings.TenantId, StringComparison.OrdinalIgnoreCase)
                          || !Guid.TryParse(user.FindFirst("oid")?.Value, out _)
                          || !settings.AllowedClientIds.Contains(user.FindFirst("azp")?.Value, StringComparer.OrdinalIgnoreCase)
                          || !(user.FindFirst("scp")?.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                              .Contains(settings.RequiredScope, StringComparer.Ordinal) ?? false))
                      {
                          context.Fail("A permitted delegated API token is required.");
                          return Task.CompletedTask;
                      }
                      var identity = (ClaimsIdentity)user.Identity!;
                      // API roles cannot masquerade as legacy web Admin claims. App-managed roles still apply.
                      foreach (Claim claim in identity.Claims.Where(c => c.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
                          || c.Type.Equals("appRole", StringComparison.OrdinalIgnoreCase)
                          || c.Type.Equals(identity.RoleClaimType, StringComparison.OrdinalIgnoreCase)
                          || c.Type.Equals(ClaimTypes.Role, StringComparison.OrdinalIgnoreCase) || c.Type == ClaimTypes.NameIdentifier
                          || c.Type == ExternalIdentityClaims.EasyAuthProviderClaimType).ToArray()) identity.RemoveClaim(claim);
                      identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, ExternalIdentityClaims.ResolveStableUserId(user.Claims)!));
                      return Task.CompletedTask;
                  }
              };
          });
        services.AddAuthorization(options => options.AddPolicy(Policy, policy =>
            policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser()));
        return services;
    }
}
