using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WriterApp.Client.Services
{
    public sealed class EasyAuthMeClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<EasyAuthMeClient> _logger;

        public EasyAuthMeClient(HttpClient http, ILogger<EasyAuthMeClient> logger)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<EasyAuthMeState> GetAsync(CancellationToken ct = default)
        {
            try
            {
                using HttpResponseMessage response = await _http.GetAsync("/.auth/me", ct);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    return EasyAuthMeState.NotSignedIn;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "EasyAuth /.auth/me returned non-success status {StatusCode}.",
                        (int)response.StatusCode);
                    return EasyAuthMeState.NotSignedIn;
                }

                List<EasyAuthIdentityDto>? identities = await response.Content.ReadFromJsonAsync<List<EasyAuthIdentityDto>>(cancellationToken: ct);
                if (identities is null || identities.Count == 0)
                {
                    return EasyAuthMeState.NotSignedIn;
                }

                EasyAuthIdentityDto identity = identities[0];
                string? displayName = FindClaim(identity.UserClaims, "name")
                    ?? FindClaim(identity.UserClaims, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")
                    ?? FindClaim(identity.UserClaims, "preferred_username");
                string? email = FindClaim(identity.UserClaims, "emails")
                    ?? FindClaim(identity.UserClaims, "email")
                    ?? FindClaim(identity.UserClaims, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")
                    ?? FindClaim(identity.UserClaims, "preferred_username");

                return new EasyAuthMeState(
                    IsAuthenticated: true,
                    DisplayName: displayName,
                    Email: email);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query /.auth/me.");
                return EasyAuthMeState.NotSignedIn;
            }
        }

        private static string? FindClaim(List<EasyAuthClaimDto>? claims, string claimType)
        {
            if (claims is null || claims.Count == 0)
            {
                return null;
            }

            EasyAuthClaimDto? match = claims.FirstOrDefault(claim =>
                string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase));
            if (match is null || string.IsNullOrWhiteSpace(match.Value))
            {
                return null;
            }

            return match.Value.Trim();
        }

        private sealed class EasyAuthIdentityDto
        {
            [JsonPropertyName("user_claims")]
            public List<EasyAuthClaimDto>? UserClaims { get; init; }
        }

        private sealed class EasyAuthClaimDto
        {
            [JsonPropertyName("typ")]
            public string? Type { get; init; }

            [JsonPropertyName("val")]
            public string? Value { get; init; }
        }
    }

    public sealed record EasyAuthMeState(
        bool IsAuthenticated,
        string? DisplayName,
        string? Email)
    {
        public static EasyAuthMeState NotSignedIn { get; } = new(
            IsAuthenticated: false,
            DisplayName: null,
            Email: null);
    }
}
