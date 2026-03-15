using System.Collections.Generic;
using System.Security;
using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Security;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class ExternalIdentityClaimsTests
    {
        [Fact]
        public void ResolveIdentity_WithLegacyOid_PreservesLegacyUserId()
        {
            Claim[] claims =
            {
                new("oid", "11111111-2222-3333-4444-555555555555"),
                new("iss", "https://login.microsoftonline.com/tenant/v2.0"),
                new("sub", "customer-subject")
            };

            ExternalIdentityClaims.ExternalIdentityResolution? result = ExternalIdentityClaims.ResolveIdentity(claims);

            Assert.NotNull(result);
            Assert.Equal(ExternalIdentityClaims.ExternalIdentityUserIdStrategy.LegacyOid, result!.Strategy);
            Assert.Equal("11111111-2222-3333-4444-555555555555", result.UserId);
            Assert.Equal("11111111-2222-3333-4444-555555555555", ExternalIdentityClaims.ResolveStableUserId(claims));
            Assert.Equal("11111111-2222-3333-4444-555555555555", ExternalIdentityClaims.ResolveOid(claims));
        }

        [Fact]
        public void ResolveEmail_WithLegacyAadEmailClaim_UsesWsFederationEmail()
        {
            Claim[] claims =
            {
                new("oid", "11111111-2222-3333-4444-555555555555"),
                new(ClaimTypes.Email, "person@contoso.com"),
                new("name", "Contoso Person")
            };

            string? email = ExternalIdentityClaims.ResolveEmail(claims);

            Assert.Equal("person@contoso.com", email);
        }

        [Fact]
        public void ResolveIdentity_WithIssuerAndSubject_UsesExternalCanonicalId()
        {
            Claim[] claims =
            {
                new("iss", "https://ExternalTenant.ciamlogin.com/tenant-id/v2.0/"),
                new("sub", "customer-subject-123"),
                new("email", "reader@example.com"),
                new("name", "Reader")
            };

            ExternalIdentityClaims.ExternalIdentityResolution? result = ExternalIdentityClaims.ResolveIdentity(claims);

            Assert.NotNull(result);
            Assert.Equal(ExternalIdentityClaims.ExternalIdentityUserIdStrategy.ExternalIssuerSubject, result!.Strategy);
            Assert.Equal(
                "extid:https%3A%2F%2Fexternaltenant.ciamlogin.com%2Ftenant-id%2Fv2.0:customer-subject-123",
                result.UserId);
            Assert.Null(ExternalIdentityClaims.ResolveOid(claims));
        }

        [Fact]
        public void ResolveEmail_WithExternalIdPreferredUsername_UsesPreferredUsername()
        {
            Claim[] claims =
            {
                new("iss", "https://tenant.ciamlogin.com/tenant-id/v2.0"),
                new("sub", "customer-subject-123"),
                new("preferred_username", "reader@gmail.com"),
                new("name", "Reader Example")
            };

            string? email = ExternalIdentityClaims.ResolveEmail(claims);

            Assert.Equal("reader@gmail.com", email);
        }

        [Fact]
        public void ResolveEmail_WithGoogleFederatedEmailsArray_UsesFirstEmail()
        {
            Claim[] claims =
            {
                new("iss", "https://tenant.ciamlogin.com/tenant-id/v2.0"),
                new("sub", "google-subject-123"),
                new("emails", "[\"reader@gmail.com\"]"),
                new("name", "Johan")
            };

            string? email = ExternalIdentityClaims.ResolveEmail(claims);

            Assert.Equal("reader@gmail.com", email);
        }

        [Fact]
        public void ResolveIdentity_UsesNormalizedIssuerConsistently()
        {
            Claim[] firstClaims =
            {
                new("iss", "HTTPS://ExternalTenant.ciamlogin.com/tenant-id/v2.0/"),
                new("sub", "customer-subject-123")
            };
            Claim[] secondClaims =
            {
                new("iss", "https://externaltenant.ciamlogin.com/tenant-id/v2.0"),
                new("sub", " customer-subject-123 ")
            };

            string? first = ExternalIdentityClaims.ResolveStableUserId(firstClaims);
            string? second = ExternalIdentityClaims.ResolveStableUserId(secondClaims);

            Assert.Equal(first, second);
        }

        [Fact]
        public void ResolveIdentity_AvoidsCollisionsAcrossIssuers()
        {
            Claim[] firstClaims =
            {
                new("iss", "https://tenant-a.ciamlogin.com/tenant/v2.0"),
                new("sub", "same-subject")
            };
            Claim[] secondClaims =
            {
                new("iss", "https://tenant-b.ciamlogin.com/tenant/v2.0"),
                new("sub", "same-subject")
            };

            string? first = ExternalIdentityClaims.ResolveStableUserId(firstClaims);
            string? second = ExternalIdentityClaims.ResolveStableUserId(secondClaims);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotEqual(first, second);
        }

        [Fact]
        public void ResolveIdentity_WithOnlySubject_MissingIssuer_ReturnsNull()
        {
            Claim[] claims =
            {
                new("sub", "customer-subject")
            };

            ExternalIdentityClaims.ExternalIdentityResolution? result = ExternalIdentityClaims.ResolveIdentity(claims);

            Assert.Null(result);
            Assert.Null(ExternalIdentityClaims.ResolveStableUserId(claims));
        }

        [Fact]
        public void ResolveIdentity_WithSidOnly_UsesLegacySidFallback()
        {
            Claim[] claims =
            {
                new("sid", "legacy-session-id")
            };

            ExternalIdentityClaims.ExternalIdentityResolution? result = ExternalIdentityClaims.ResolveIdentity(claims);

            Assert.NotNull(result);
            Assert.Equal(ExternalIdentityClaims.ExternalIdentityUserIdStrategy.LegacySid, result!.Strategy);
            Assert.Equal("legacy-session-id", result.UserId);
        }

        [Fact]
        public void UserIdResolver_Throws_WhenCanonicalIdentityClaimsAreMissing()
        {
            ClaimsPrincipal principal = new(new ClaimsIdentity(new[]
            {
                new Claim("email", "reader@example.com")
            }, authenticationType: "Test"));

            UserIdResolver resolver = new(NullLogger<UserIdResolver>.Instance);

            SecurityException ex = Assert.Throws<SecurityException>(() => resolver.ResolveUserId(principal));

            Assert.Contains("canonical identity claims", ex.Message);
        }
    }
}
