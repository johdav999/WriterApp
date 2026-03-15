using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using WriterApp.Data;

namespace WriterApp.Application.Security
{
    public static class ExternalIdentityClaims
    {
        private const string ExternalIdentityPrefix = "extid";
        public const string EasyAuthProviderClaimType = "easyauth:provider";

        private static readonly string[] OidClaimTypes =
        {
            "oid",
            "http://schemas.microsoft.com/identity/claims/objectidentifier"
        };

        private static readonly string[] SubjectClaimTypes =
        {
            "sub"
        };

        private static readonly string[] IssuerClaimTypes =
        {
            "iss",
            "issuer"
        };

        private static readonly string[] SidClaimTypes =
        {
            "sid",
            ClaimTypes.Sid
        };

        private static readonly string[] EmailClaimTypes =
        {
            ClaimTypes.Email,
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
            "email",
            "emails",
            "preferred_username",
            ClaimTypes.Upn,
            "upn",
            "unique_name"
        };

        private static readonly string[] NameClaimTypes =
        {
            ClaimTypes.Name,
            "name",
            "given_name",
            ClaimTypes.GivenName,
            "preferred_username",
            "unique_name"
        };

        public static string? ResolveStableUserId(IEnumerable<Claim> claims)
        {
            return ResolveIdentity(claims)?.UserId;
        }

        public static string? ResolveOid(IEnumerable<Claim> claims)
        {
            return NormalizeSimpleClaim(ResolveFirstValue(claims, OidClaimTypes));
        }

        public static string? ResolveIssuer(IEnumerable<Claim> claims)
        {
            return NormalizeIssuer(ResolveFirstValue(claims, IssuerClaimTypes));
        }

        public static string? ResolveSubject(IEnumerable<Claim> claims)
        {
            return NormalizeSimpleClaim(ResolveFirstValue(claims, SubjectClaimTypes));
        }

        public static string? ResolveSid(IEnumerable<Claim> claims)
        {
            return NormalizeSimpleClaim(ResolveFirstValue(claims, SidClaimTypes));
        }

        public static string? ResolveEasyAuthProvider(IEnumerable<Claim> claims)
        {
            return NormalizeSimpleClaim(ResolveFirstValue(claims, EasyAuthProviderClaimType));
        }

        public static ExternalIdentityResolution? ResolveIdentity(IEnumerable<Claim> claims)
        {
            string? oid = ResolveOid(claims);
            if (!string.IsNullOrWhiteSpace(oid))
            {
                return new ExternalIdentityResolution(
                    oid,
                    ExternalIdentityUserIdStrategy.LegacyOid,
                    oid,
                    ResolveIssuer(claims),
                    ResolveSubject(claims));
            }

            string? issuer = ResolveIssuer(claims);
            string? subject = ResolveSubject(claims);
            if (!string.IsNullOrWhiteSpace(issuer) && !string.IsNullOrWhiteSpace(subject))
            {
                string encodedIssuer = Uri.EscapeDataString(issuer);
                string encodedSubject = Uri.EscapeDataString(subject);
                string canonicalUserId = $"{ExternalIdentityPrefix}:{encodedIssuer}:{encodedSubject}";
                return new ExternalIdentityResolution(
                    canonicalUserId,
                    ExternalIdentityUserIdStrategy.ExternalIssuerSubject,
                    null,
                    issuer,
                    subject);
            }

            string? sid = ResolveSid(claims);
            if (!string.IsNullOrWhiteSpace(sid))
            {
                return new ExternalIdentityResolution(
                    sid,
                    ExternalIdentityUserIdStrategy.LegacySid,
                    null,
                    issuer,
                    subject);
            }

            return null;
        }

        public static string? ResolveEmail(IEnumerable<Claim> claims)
        {
            string? direct = ResolveFirstStructuredValue(claims, EmailClaimTypes, LooksLikeEmail);
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

        public static IReadOnlyList<string> DescribePresentEmailClaimTypes(IEnumerable<Claim> claims)
        {
            return DescribePresentClaimTypes(claims, EmailClaimTypes);
        }

        public static IReadOnlyList<string> DescribePresentNameClaimTypes(IEnumerable<Claim> claims)
        {
            return DescribePresentClaimTypes(claims, NameClaimTypes);
        }

        // External ID -> internal UserProfile mapping:
        // legacy workforce oid -> UserProfile.UserId
        // external customer issuer+subject -> extid:{escaped_issuer}:{escaped_subject}
        // name/email (fallback) -> UserProfile.DisplayName
        public static UserProfileIdentity MapToUserProfileIdentity(IEnumerable<Claim> claims, string fallbackUserId)
        {
            string userId = ResolveIdentity(claims)?.UserId ?? fallbackUserId;
            return new UserProfileIdentity(
                userId,
                ResolveEmail(claims),
                ResolveDisplayName(claims, userId));
        }

        public static ExternalIdentityLinkIdentity MapToExternalIdentityLinkIdentity(IEnumerable<Claim> claims)
        {
            return new ExternalIdentityLinkIdentity(
                ResolveEasyAuthProvider(claims),
                ResolveIssuer(claims),
                ResolveSubject(claims),
                ResolveOid(claims),
                ResolveEmail(claims));
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

        private static string? ResolveFirstStructuredValue(
            IEnumerable<Claim> claims,
            IEnumerable<string> claimTypes,
            Func<string, bool> predicate)
        {
            if (claims is null)
            {
                return null;
            }

            HashSet<string> claimTypeSet = new(claimTypes, StringComparer.OrdinalIgnoreCase);
            foreach (Claim claim in claims)
            {
                if (!claimTypeSet.Contains(claim.Type))
                {
                    continue;
                }

                foreach (string candidate in ExpandClaimValues(claim.Value))
                {
                    if (predicate(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static string? NormalizeSimpleClaim(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string? NormalizeIssuer(string? issuer)
        {
            if (string.IsNullOrWhiteSpace(issuer))
            {
                return null;
            }

            string trimmed = issuer.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? absolute))
            {
                string normalized = absolute.GetLeftPart(UriPartial.Authority) + absolute.AbsolutePath;
                return normalized.TrimEnd('/').ToLowerInvariant();
            }

            return trimmed.TrimEnd('/').ToLowerInvariant();
        }

        private static bool LooksLikeEmail(string? candidate)
        {
            return !string.IsNullOrWhiteSpace(candidate) && candidate.Contains('@', StringComparison.Ordinal);
        }

        private static IEnumerable<string> ExpandClaimValues(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                yield break;
            }

            string trimmed = rawValue.Trim();
            if (TryParseJsonArray(trimmed, out IReadOnlyList<string>? items))
            {
                foreach (string item in items)
                {
                    if (!string.IsNullOrWhiteSpace(item))
                    {
                        yield return item.Trim();
                    }
                }

                yield break;
            }

            if (trimmed.Contains(';', StringComparison.Ordinal) || trimmed.Contains(',', StringComparison.Ordinal))
            {
                foreach (string part in trimmed.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!string.IsNullOrWhiteSpace(part))
                    {
                        yield return part;
                    }
                }

                yield break;
            }

            yield return trimmed;
        }

        private static bool TryParseJsonArray(string value, out IReadOnlyList<string>? items)
        {
            items = null;
            if (!value.StartsWith("[", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(value);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                items = doc.RootElement
                    .EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToArray();
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static IReadOnlyList<string> DescribePresentClaimTypes(IEnumerable<Claim> claims, IEnumerable<string> candidateTypes)
        {
            if (claims is null)
            {
                return Array.Empty<string>();
            }

            HashSet<string> candidateSet = new(candidateTypes, StringComparer.OrdinalIgnoreCase);
            return claims
                .Where(claim => candidateSet.Contains(claim.Type))
                .Select(claim => claim.Type)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(type => type, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string DescribeResolutionStrategy(IEnumerable<Claim> claims)
        {
            ExternalIdentityResolution? resolution = ResolveIdentity(claims);
            if (resolution is null)
            {
                return "MissingClaims";
            }

            return resolution.Strategy.ToString();
        }

        public static string MaskUserId(string? userId)
        {
            string normalized = IdNorm.Norm(userId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            int length = Math.Min(8, normalized.Length);
            return $"***{normalized[^length..]}";
        }

        public static string? NormalizeEmail(string? email)
        {
            return string.IsNullOrWhiteSpace(email)
                ? null
                : email.Trim().ToLowerInvariant();
        }

        public static string MaskEmail(string? email)
        {
            string? normalized = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            int atIndex = normalized.IndexOf('@');
            if (atIndex <= 0 || atIndex == normalized.Length - 1)
            {
                return "***";
            }

            string local = normalized[..atIndex];
            string domain = normalized[(atIndex + 1)..];
            string maskedLocal = local.Length <= 2
                ? new string('*', local.Length)
                : $"{local[0]}***{local[^1]}";
            return $"{maskedLocal}@{domain}";
        }

        public enum ExternalIdentityUserIdStrategy
        {
            MissingClaims = 0,
            LegacyOid = 1,
            ExternalIssuerSubject = 2,
            LegacySid = 3
        }

        public sealed record UserProfileIdentity(string UserId, string? Email, string DisplayName);
        public sealed record ExternalIdentityLinkIdentity(string? Provider, string? Issuer, string? Subject, string? ObjectIdentifier, string? EmailAtLinkTime);

        public sealed record ExternalIdentityResolution(
            string UserId,
            ExternalIdentityUserIdStrategy Strategy,
            string? Oid,
            string? Issuer,
            string? Subject);
    }
}
