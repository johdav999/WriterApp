using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using WriterApp.Application.Security;

namespace WriterApp.Application.Collaboration;

// Public-client code/PKCE with the native app's registered localhost redirect.
// Tokens never enter browser storage or the local account cookie.
public sealed class LocalCollaborationSignIn(IOptionsMonitor<JwtBearerOptions> bearer,IConfiguration config,IDataProtectionProvider protection,IHttpClientFactory clients)
{
    private string CorrelationCookie=>LocalCollaborationTest.AppPort(config)==5398?"Prosa.LocalCollaboration.SignIn":$"Prosa.LocalCollaboration.SignIn.{LocalCollaborationTest.AppPort(config)}";
    private sealed record Flow(string State,string Verifier,long Expires,string ExpectedEmail,string? ReturnPath=null);
    private static string ReturnPath(string? candidate) {
        var safe=WriterApp.Shared.ReturnUrlSafety.NormalizeOrFallback(candidate,"/collaboration/test");
        return safe.StartsWith("/app/",StringComparison.Ordinal) && !safe.StartsWith("/app/login",StringComparison.OrdinalIgnoreCase)
            && !safe.StartsWith("/app/register",StringComparison.OrdinalIgnoreCase) ? safe : "/collaboration/test";
    }
    private static string FailurePath(string destination,string failure)=>destination=="/collaboration/test"
        ? "/collaboration/test?signin="+failure
        : QueryHelpers.AddQueryString("/app/login",new Dictionary<string,string?>{{"returnUrl",destination},{"signin",failure}});
    private IDataProtector Protector=>LocalCollaborationTest.AppPort(config)==5398
        ? protection.CreateProtector("Prosa.LocalCollaboration.PKCE.v1")
        : protection.CreateProtector("Prosa.LocalCollaboration.PKCE.v1",LocalCollaborationTest.LocalOrigin(config));
    private NativeBearerOptions Settings=>config.GetSection("NativeAuth").Get<NativeBearerOptions>()??new();
    private static string Random()=>WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    private static CookieOptions Cookies=>new(){HttpOnly=true,SameSite=SameSiteMode.Lax,Path="/",MaxAge=TimeSpan.FromMinutes(5)};
    public async Task Begin(HttpContext context)
    {
        context.Response.Headers.CacheControl="no-store";context.Response.Headers["Referrer-Policy"]="no-referrer";
        var email=context.Request.Query["email"].ToString().Trim();
        string destination=ReturnPath(context.Request.Query["returnUrl"]);
        if(email.Length>254||!System.Net.Mail.MailAddress.TryCreate(email,out var parsed)||parsed.Address!=email){
            context.Response.Redirect(FailurePath(destination,"email-required"));return;
        }
        var settings=Settings;settings.Validate();
        var metadata=await bearer.Get(NativeBearerAuthentication.Scheme).ConfigurationManager!.GetConfigurationAsync(context.RequestAborted);
        var flow=new Flow(Random(),Random(),DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),email,destination);
        context.Response.Cookies.Append(CorrelationCookie,Protector.Protect(JsonSerializer.Serialize(flow)),Cookies);
        context.Response.Redirect(QueryHelpers.AddQueryString(metadata.AuthorizationEndpoint,new Dictionary<string,string?>{
            ["client_id"]=settings.AllowedClientIds[0],["response_type"]="code",["redirect_uri"]=LocalCollaborationTest.LocalOrigin(config)+"/",
            ["response_mode"]="query",["scope"]=$"openid profile email api://{settings.Audience}/{settings.RequiredScope}",
            ["state"]=flow.State,["code_challenge"]=WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(flow.Verifier))),
            ["code_challenge_method"]="S256",["prompt"]="login",["login_hint"]=email}));
    }
    public async Task Complete(HttpContext context)
    {
        context.Response.Headers.CacheControl="no-store";context.Response.Headers["Referrer-Policy"]="no-referrer";
        string failure="failed";
        string destination="/collaboration/test";
        try
        {
            var flow=JsonSerializer.Deserialize<Flow>(Protector.Unprotect(context.Request.Cookies[CorrelationCookie]??""))!;
            destination=ReturnPath(flow.ReturnPath);
            var state=context.Request.Query["state"].ToString();var code=context.Request.Query["code"].ToString();
            if(flow.Expires<DateTimeOffset.UtcNow.ToUnixTimeSeconds()||state.Length>128||code.Length is 0 or >8192
                ||!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(flow.State),Encoding.UTF8.GetBytes(state)))throw new InvalidOperationException();
            var options=bearer.Get(NativeBearerAuthentication.Scheme);var metadata=await options.ConfigurationManager!.GetConfigurationAsync(context.RequestAborted);
            var settings=Settings;
            if(!Uri.TryCreate(metadata.TokenEndpoint,UriKind.Absolute,out var tokenUri)||tokenUri.Scheme!="https")throw new InvalidOperationException();
            using var http=clients.CreateClient();using var timeout=CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);timeout.CancelAfter(TimeSpan.FromSeconds(20));
            using var response=await http.PostAsync(metadata.TokenEndpoint,new FormUrlEncodedContent(new Dictionary<string,string>{
                ["client_id"]=settings.AllowedClientIds[0],["grant_type"]="authorization_code",["code"]=code,
                ["redirect_uri"]=LocalCollaborationTest.LocalOrigin(config)+"/",["code_verifier"]=flow.Verifier}),timeout.Token);
            if(!response.IsSuccessStatusCode)throw new InvalidOperationException();
            using var payload=JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.RequestAborted));
            var token=payload.RootElement.GetProperty("access_token").GetString();if(token is null||token.Length>32768)throw new InvalidOperationException();
            var parameters=options.TokenValidationParameters.Clone();parameters.IssuerSigningKeys=metadata.SigningKeys;
            var validated=await new JsonWebTokenHandler{MapInboundClaims=false}.ValidateTokenAsync(token,parameters);
            if(!validated.IsValid)throw new InvalidOperationException();
            var principal=new ClaimsPrincipal(validated.ClaimsIdentity);
            if(!NativeBearerAuthentication.NormalizeDelegatedPrincipal(principal,settings))throw new InvalidOperationException();
            // This is an account-selection guard, never an authentication or account-linking key.
            var returnedEmail=ExternalIdentityClaims.ResolveEmail(principal.Claims);
            if(string.IsNullOrWhiteSpace(flow.ExpectedEmail)||!string.Equals(returnedEmail,flow.ExpectedEmail,StringComparison.OrdinalIgnoreCase)){
                failure=string.IsNullOrWhiteSpace(returnedEmail)?"email-unavailable":"account-mismatch";throw new InvalidOperationException();
            }
            long expiry=long.Parse(principal.FindFirst("exp")!.Value,System.Globalization.CultureInfo.InvariantCulture);
            await context.SignInAsync(LocalCollaborationTest.CookieScheme,principal,new AuthenticationProperties{
                IsPersistent=false,ExpiresUtc=DateTimeOffset.FromUnixTimeSeconds(expiry),AllowRefresh=false});
            context.Response.Redirect(destination);
        }
        catch { context.Response.Redirect(FailurePath(destination,failure)); }
        finally { context.Response.Cookies.Delete(CorrelationCookie,new CookieOptions{Path="/"}); }
    }
}

public static class LocalCollaborationEndpoints
{
    public static void UseLocalCollaborationTest(this WebApplication app)
    {
        app.Use(async(context,next)=>{
            if(!LocalCollaborationTest.AcceptsRequest(context,app.Configuration)){context.Response.StatusCode=403;return;}
            if(context.Request.Path=="/"&&context.Request.Query.ContainsKey("state")){
                await context.RequestServices.GetRequiredService<LocalCollaborationSignIn>().Complete(context);return;
            }
            await next(context);
        });
        app.MapGet("/local-collaboration/sign-in-mode",(HttpContext ctx)=>{ctx.Response.Headers.CacheControl="no-store";return Results.Ok(new {enabled=true});}).AllowAnonymous();
        app.MapGet("/local-collaboration/sign-in",async(HttpContext ctx,LocalCollaborationSignIn signIn)=>await signIn.Begin(ctx)).AllowAnonymous();
        app.MapPost("/local-collaboration/sign-out",async(HttpContext ctx)=>{
            await ctx.SignOutAsync(LocalCollaborationTest.CookieScheme);return Results.Ok();
        }).RequireAuthorization();
        app.MapGet("/collaboration/test",()=>Results.File(Path.Combine(app.Environment.ContentRootPath,"WriterApp.Collaboration/local-test/index.html"),"text/html")).AllowAnonymous();
        app.MapGet("/collaboration/test-client.js",()=>Results.File(Path.Combine(app.Environment.ContentRootPath,"WriterApp.Collaboration/local-test/client.mjs"),"text/javascript")).AllowAnonymous();
        app.MapGet("/collaboration/test-editor.js",()=>Results.File(Path.Combine(app.Environment.ContentRootPath,"WriterApp.Client/wwwroot/js/tiptap-editor.bundle.js"),"text/javascript")).AllowAnonymous();
    }
}
