using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Security;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalCollaborationSignInTests
{
    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("Someone <fixture@example.test>")]
    public async Task SignInRequiresAnExplicitPlainEmail(string email)
    {
        var signIn=new LocalCollaborationSignIn(null!,new ConfigurationBuilder().Build(),new EphemeralDataProtectionProvider(),null!);
        var context=new DefaultHttpContext();context.Request.QueryString=QueryString.Create("email",email);
        await signIn.Begin(context);
        Assert.Equal("/collaboration/test?signin=email-required",context.Response.Headers.Location.ToString());
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Theory]
    [InlineData("valid",true)]
    [InlineData("normal-editor",true)]
    [InlineData("alternate-port",true)]
    [InlineData("state",false)]
    [InlineData("expired-state",false)]
    [InlineData("issuer",false)]
    [InlineData("audience",false)]
    [InlineData("client",false)]
    [InlineData("scope",false)]
    [InlineData("expired-token",false)]
    [InlineData("unsigned",false)]
    [InlineData("account-mismatch",false)]
    [InlineData("missing-email-claim",false)]
    [InlineData("preferred-username",true)]
    [InlineData("preferred-username-mismatch",false)]
    public async Task LocalCookieRequiresBoundPkceFlowAndValidDelegatedProsaToken(string change,bool expected)
    {
        const string tenant="a000a187-b940-41c1-bebe-f30c4a351099",client="323361e9-8562-43aa-a558-6b4d3cbbb6a2",audience="c0ecc793-e620-416c-a072-4e9eca30f048";
        const string issuer="https://issuer.example/v2.0";
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["NativeAuth:Enabled"]="true",["NativeAuth:Authority"]="https://issuer.example/v2.0",["NativeAuth:Issuer"]=issuer,
            ["NativeAuth:TenantId"]=tenant,["NativeAuth:Audience"]=audience,["NativeAuth:AllowedClientIds:0"]=client}).Build();
        var settings=config.GetSection("NativeAuth").Get<NativeBearerOptions>()!;
        if(change=="alternate-port")config["Collaboration:LocalTest:Port"]="5407";
        using var rsa=RSA.Create(2048);var key=new RsaSecurityKey(rsa){KeyId="fixture-key"};
        var claims=new Dictionary<string,object>{{"ver","2.0"},{"tid",tenant},{"oid",Guid.NewGuid().ToString()},
            {"azp",change=="client"?Guid.NewGuid().ToString():client},{"scp",change=="scope"?"wrong":settings.RequiredScope},
            {"email","fixture@example.test"},{"roles",new[]{"Admin"}}};
        if(change=="missing-email-claim")claims.Remove("email");
        if(change is "preferred-username" or "preferred-username-mismatch"){
            claims.Remove("email");claims["preferred_username"]=change=="preferred-username-mismatch"?"different@example.test":"fixture@example.test";
        }
        var descriptor=new SecurityTokenDescriptor{Issuer=change=="issuer"?"https://other.example/v2.0":issuer,
            Audience=change=="audience"?Guid.NewGuid().ToString():audience,Claims=claims,
            IssuedAt=DateTime.UtcNow.AddMinutes(-10),NotBefore=DateTime.UtcNow.AddMinutes(-10),
            Expires=change=="expired-token"?DateTime.UtcNow.AddMinutes(-5):DateTime.UtcNow.AddMinutes(5),
            SigningCredentials=change=="unsigned"?null:new SigningCredentials(key,SecurityAlgorithms.RsaSha256)};
        var token=new JsonWebTokenHandler().CreateToken(descriptor);
        var handler=new TokenEndpoint(token);
        var protection=new EphemeralDataProtectionProvider();var services=new ServiceCollection();
        services.AddLogging();services.AddSingleton<IConfiguration>(config);services.AddSingleton<IDataProtectionProvider>(protection);
        services.AddSingleton<IHttpClientFactory>(new Clients(handler));
        services.AddAuthentication().AddCookie(LocalCollaborationTest.CookieScheme,o=>o.Cookie.Name="Prosa.LocalCollaboration");
        services.AddNativeBearerAuthentication(settings,LocalCollaborationTest.CookieScheme);
        using var provider=services.BuildServiceProvider();
        var options=provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var metadata=new OpenIdConnectConfiguration{AuthorizationEndpoint="https://issuer.example/authorize",TokenEndpoint="https://issuer.example/token",Issuer=issuer};metadata.SigningKeys.Add(key);
        options.Get(NativeBearerAuthentication.Scheme).ConfigurationManager=new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
        var signIn=new LocalCollaborationSignIn(options,config,protection,provider.GetRequiredService<IHttpClientFactory>());
        string? destination=change=="normal-editor"?"/app/documents/fixture?tab=history":null;
        var start=Context(provider);start.Request.QueryString=QueryString.Create(new Dictionary<string,string?>{{"email",change=="account-mismatch"?"other@example.test":"FIXTURE@example.test"},{"returnUrl",destination}});await signIn.Begin(start);
        var query=QueryHelpers.ParseQuery(new Uri(start.Response.Headers.Location.ToString()).Query);
        Assert.Equal("S256",query["code_challenge_method"]);Assert.Equal((change=="alternate-port"?"http://localhost:5407":LocalCollaborationTest.Origin)+"/",query["redirect_uri"]);
        Assert.Equal("login",query["prompt"]);Assert.Equal(change=="account-mismatch"?"other@example.test":"FIXTURE@example.test",query["login_hint"]);
        string cookie=start.Response.Headers.SetCookie.Single()!.Split(';')[0],protectedFlow=cookie[(cookie.IndexOf('=')+1)..];
        if(change=="expired-state"){
            var protector=protection.CreateProtector("Prosa.LocalCollaboration.PKCE.v1");
            var flow=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(protector.Unprotect(protectedFlow))!;
            flow["Expires"]=JsonSerializer.SerializeToElement(0L);cookie=cookie[..(cookie.IndexOf('=')+1)]+protector.Protect(JsonSerializer.Serialize(flow));
        }
        var callback=Context(provider);callback.Request.Headers.Cookie=cookie;
        callback.Request.QueryString=QueryString.Create(new Dictionary<string,string?>{{"code","owned-fixture-code"},{"state",change=="state"?"wrong":query["state"]},{"returnUrl","https://untrusted.example/"}});
        await signIn.Complete(callback);
        var failure=change=="missing-email-claim"?"email-unavailable":change is "account-mismatch" or "preferred-username-mismatch"?"account-mismatch":"failed";
        Assert.Equal(expected?destination??"/collaboration/test":"/collaboration/test?signin="+failure,callback.Response.Headers.Location.ToString());
        var authCookie=callback.Response.Headers.SetCookie.FirstOrDefault(c=>c!.StartsWith("Prosa.LocalCollaboration=",StringComparison.Ordinal));
        Assert.Equal(expected,authCookie is not null);
        if(expected){
            Assert.DoesNotContain(token,authCookie!);Assert.Contains("httponly",authCookie!,StringComparison.OrdinalIgnoreCase);
            var form=QueryHelpers.ParseQuery(handler.Form!);
            Assert.Equal(query["redirect_uri"],form["redirect_uri"]);
            Assert.Equal(query["code_challenge"],WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"]!))));
            var cookieOptions=provider.GetRequiredService<IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>().Get(LocalCollaborationTest.CookieScheme);
            var rawCookie=authCookie!.Split(';')[0];var ticket=cookieOptions.TicketDataFormat.Unprotect(rawCookie[(rawCookie.IndexOf('=')+1)..]);
            Assert.NotNull(ticket);Assert.True(ticket.Principal.Identity!.IsAuthenticated);
            Assert.DoesNotContain(ticket.Principal.Claims,c=>c.Type=="roles"||c.Type==ClaimTypes.Role);
            Assert.NotNull(ticket.Principal.FindFirst(ClaimTypes.NameIdentifier));
        }
        else if(change is "state" or "expired-state")Assert.Null(handler.Form);
    }

    [Theory]
    [InlineData("Production",false)]
    [InlineData("Development",false)]
    [InlineData("Staging",true)]
    public void ActivationRequiresStagingAndEveryServerGate(string environment,bool expected)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["Collaboration:LocalTest:Enabled"]="true",["Collaboration:SessionsEnabled"]="true",["Collaboration:CommitsEnabled"]="true",
            ["Collaboration:Sharing:Enabled"]="true",["Collaboration:Conversion:Enabled"]="true",["Collaboration:Environment"]="test",
            ["Collaboration:WebSocketUrl"]="ws://localhost:1234/",["Collaboration:ServiceKey"]=new string('x',32)}).Build();
        var host=new HostEnvironment{EnvironmentName=environment};var gate=new CollaborationStagedTestGate(host,config);
        Assert.Equal(expected,gate.Enabled);config["Collaboration:CommitsEnabled"]="false";Assert.False(gate.Enabled);
    }
    private static DefaultHttpContext Context(IServiceProvider services){var context=new DefaultHttpContext{RequestServices=services};context.Request.Scheme="http";context.Request.Host=new("localhost",5398);return context;}
    private sealed class Clients(HttpMessageHandler handler):IHttpClientFactory{public HttpClient CreateClient(string name)=>new(handler,false);}
    private sealed class TokenEndpoint(string token):HttpMessageHandler
    {
        public string? Form;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Form=await request.Content!.ReadAsStringAsync(ct);return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{access_token=token}))};}
    }
    private sealed class HostEnvironment:IHostEnvironment
    {
        public string EnvironmentName{get;set;}="Staging";public string ApplicationName{get;set;}="Test";public string ContentRootPath{get;set;}=Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider{get;set;}=new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
