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
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string headerValue = Request.Headers[HeaderName].ToString();
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

                if (root.TryGetProperty("auth_typ", out JsonElement authTypeElement))
                {
                    string? authType = authTypeElement.GetString();
                    if (!string.IsNullOrWhiteSpace(authType))
                    {
                        claims.Add(new Claim(ExternalIdentityClaims.EasyAuthProviderClaimType, authType.Trim()));
                    }
                }

                ExternalIdentityClaims.ExternalIdentityResolution? identityResolution =
                    ExternalIdentityClaims.ResolveIdentity(claims);
                string? canonicalUserId = identityResolution?.UserId;
                if (!string.IsNullOrWhiteSpace(canonicalUserId))
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, canonicalUserId));
                }

                string? email = ExternalIdentityClaims.ResolveEmail(claims);
                if (!string.IsNullOrWhiteSpace(email))
                {
                    claims.Add(new Claim(ClaimTypes.Email, email));
                }

                string displayName = ExternalIdentityClaims.ResolveDisplayName(claims, canonicalUserId ?? "unknown");
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    claims.Add(new Claim(ClaimTypes.Name, displayName));
                }

                Logger.LogInformation(
                    "EasyAuth claims resolved. Strategy={Strategy} EmailResolved={EmailResolved} DisplayNameResolved={DisplayNameResolved} EmailClaimTypes={EmailClaimTypes} NameClaimTypes={NameClaimTypes} ClaimCount={ClaimCount}",
                    identityResolution?.Strategy.ToString() ?? ExternalIdentityClaims.ExternalIdentityUserIdStrategy.MissingClaims.ToString(),
                    !string.IsNullOrWhiteSpace(email),
                    !string.IsNullOrWhiteSpace(displayName),
                    ExternalIdentityClaims.DescribePresentEmailClaimTypes(claims),
                    ExternalIdentityClaims.DescribePresentNameClaimTypes(claims),
                    claims.Count);

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
