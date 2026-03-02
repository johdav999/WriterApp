using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WriterApp.Application.Security
{
    public sealed class LocalDevAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "LocalDev";

        public LocalDevAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            List<Claim> claims = new()
            {
                new Claim(ClaimTypes.NameIdentifier, "dev-oid"),
                new Claim("oid", "dev-oid"),
                new Claim("sub", "dev-oid"),
                new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", "dev-oid"),
                new Claim(ClaimTypes.Email, "dev@local"),
                new Claim("email", "dev@local"),
                new Claim(ClaimTypes.Name, "Dev User"),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("role", "Admin"),
                new Claim("roles", "Admin")
            };

            ClaimsIdentity identity = new(claims, SchemeName);
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
