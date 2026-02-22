using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace WriterApp.Application.Security
{
    public static class ExternalIdentityClaims
    {
        private static readonly string[] OidClaimTypes =
        {
            "oid",
            "http://schemas.microsoft.com/identity/claims/objectidentifier"
        };

        private static readonly string[] EmailClaimTypes =
        {
            ClaimTypes.Email,
            "email",
            "emails",
            "preferred_username",
            ClaimTypes.Upn
        };

        private static readonly string[] NameClaimTypes =
        {
            ClaimTypes.Name,
            "name",
            "given_name"
        };

        public static string? ResolveOid(IEnumerable<Claim> claims)
        {
            return ResolveFirstValue(claims, OidClaimTypes);
        }

        public static string? ResolveEmail(IEnumerable<Claim> claims)
        {
            string? direct = ResolveFirstValue(claims, EmailClaimTypes);
            if (!string.IsNullOrWhiteSpace(direct))
            {
                return direct;
            }

            string? identityName = ResolveFirstValue(claims, ClaimTypes.Name);
            return LooksLikeEmail(identityName) ? identityName : null;
        }

        public static string ResolveDisplayName(IEnumerable<Claim> claims, string fallbackUserId)
        {
            string? name = ResolveFirstValue(claims, NameClaimTypes);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            string? email = ResolveEmail(claims);
            if (!string.IsNullOrWhiteSpace(email))
            {
                return email;
            }

            return fallbackUserId;
        }

        // External ID -> internal UserProfile mapping:
        // oid -> UserProfile.UserId
        // name/email (fallback) -> UserProfile.DisplayName
        public static UserProfileIdentity MapToUserProfileIdentity(IEnumerable<Claim> claims, string fallbackUserId)
        {
            string userId = ResolveOid(claims) ?? fallbackUserId;
            return new UserProfileIdentity(
                userId,
                ResolveEmail(claims),
                ResolveDisplayName(claims, userId));
        }

        private static string? ResolveFirstValue(IEnumerable<Claim> claims, params string[] claimTypes)
        {
            if (claims is null)
            {
                return null;
            }

            foreach (string claimType in claimTypes)
            {
                string? value = claims.FirstOrDefault(c => string.Equals(c.Type, claimType, StringComparison.OrdinalIgnoreCase))?.Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private static bool LooksLikeEmail(string? candidate)
        {
            return !string.IsNullOrWhiteSpace(candidate) && candidate.Contains('@', StringComparison.Ordinal);
        }

        public sealed record UserProfileIdentity(string UserId, string? Email, string DisplayName);
    }
}
