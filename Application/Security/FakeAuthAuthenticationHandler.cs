using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WriterApp.Application.Security
{
    public sealed class FakeAuthAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "FakeAuth";
        private const string DefaultOid = "dev-oid";
        private readonly IConfiguration _configuration;

        public FakeAuthAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IConfiguration configuration)
            : base(options, logger, encoder)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string oid = _configuration["DEV_AUTH_OID"] ?? DefaultOid;
            string name = _configuration["DEV_AUTH_NAME"] ?? "dev@local";
            string email = _configuration["DEV_AUTH_EMAIL"]
                ?? (name.Contains('@', StringComparison.Ordinal) ? name : $"{name}@local.test");
            bool isAdmin = string.Equals(
                _configuration["DEV_AUTH_ADMIN"],
                "true",
                StringComparison.OrdinalIgnoreCase);

            List<Claim> claims = new()
            {
                new Claim("oid", oid),
                new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", oid),
                new Claim(ClaimTypes.NameIdentifier, oid),
                new Claim(ClaimTypes.Name, name),
                new Claim(ClaimTypes.Email, email),
                new Claim("email", email)
            };

            if (isAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }

            ClaimsIdentity identity = new(claims, SchemeName);
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
