using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WriterApp.Application.Security
{
    public sealed class EasyAuthAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "EasyAuth";
        private const string HeaderName = "X-MS-CLIENT-PRINCIPAL";

        public EasyAuthAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string headerValue = Request.Headers[HeaderName];
            if (string.IsNullOrWhiteSpace(headerValue))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            try
            {
                byte[] decodedBytes = Convert.FromBase64String(headerValue);
                string json = Encoding.UTF8.GetString(decodedBytes);

                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                List<Claim> claims = new();

                if (root.TryGetProperty("claims", out JsonElement claimsElement)
                    && claimsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement claimElement in claimsElement.EnumerateArray())
                    {
                        if (!claimElement.TryGetProperty("typ", out JsonElement typeElement)
                            || !claimElement.TryGetProperty("val", out JsonElement valueElement))
                        {
                            continue;
                        }

                        string? type = typeElement.GetString();
                        string? value = valueElement.GetString();
                        if (!string.IsNullOrWhiteSpace(type) && value is not null)
                        {
                            claims.Add(new Claim(type, value));
                        }
                    }
                }

                if (root.TryGetProperty("name", out JsonElement nameElement))
                {
                    string? name = nameElement.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        claims.Add(new Claim(ClaimTypes.Name, name));
                    }
                }

                ClaimsIdentity identity = new(claims, SchemeName);
                ClaimsPrincipal principal = new(identity);
                AuthenticationTicket ticket = new(principal, SchemeName);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to parse {HeaderName} header.", HeaderName);
                return Task.FromResult(AuthenticateResult.Fail("Invalid EasyAuth header."));
            }
        }
    }
}
