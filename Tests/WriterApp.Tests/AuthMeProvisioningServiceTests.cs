using System;
using System.Security;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Application.Usage;
using WriterApp.Data;
using WriterApp.Data.Security;
using WriterApp.Data.Subscriptions;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class AuthMeProvisioningServiceTests
    {
        [Fact]
        public async Task ProvisionAsync_FirstLoginForExternalIdCustomer_CreatesProfileAndEntitlement()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            AuthMeProvisioningService service = BuildService(dbContext);
            ClaimsPrincipal principal = BuildPrincipal(
                new Claim(ExternalIdentityClaims.EasyAuthProviderClaimType, "externalid"),
                new Claim("iss", "https://tenant.ciamlogin.com/tenant-id/v2.0"),
                new Claim("sub", "customer-123"),
                new Claim("email", "reader@gmail.com"),
                new Claim("name", "Reader Example"));
            string userId = "extid:https%3A%2F%2Ftenant.ciamlogin.com%2Ftenant-id%2Fv2.0:customer-123";

            AuthMeProvisioningResult result = await service.ProvisionAsync(principal, userId, CancellationToken.None);

            Assert.True(result.CreatedProfile);
            Assert.True(result.CreatedEntitlement);
            Assert.Equal("reader@gmail.com", result.ProfileIdentity.Email);
            Assert.Equal("Reader Example", result.ProfileIdentity.DisplayName);

            UserProfile profile = await dbContext.UserProfiles.SingleAsync(item => item.UserId == userId);
            Assert.Equal("reader@gmail.com", profile.Email);
            Assert.Equal("Reader Example", profile.DisplayName);
            Assert.False(profile.HasOnboarded);

            UserEntitlement entitlement = await dbContext.UserEntitlements.SingleAsync(item => item.UserId == userId);
            Assert.Equal(UserEntitlementDefaults.FreePlanKey, entitlement.PlanKey);
            Assert.Equal(UserEntitlementDefaults.FREE_MONTHLY_TOKEN_BUDGET, entitlement.AiMonthlyTokenBudget);

            ExternalIdentityLink link = await dbContext.ExternalIdentityLinks.SingleAsync(item => item.UserId == userId);
            Assert.Equal("externalid", link.Provider);
            Assert.Equal("https://tenant.ciamlogin.com/tenant-id/v2.0", link.Issuer);
            Assert.Equal("customer-123", link.Subject);
        }

        [Fact]
        public async Task ProvisionAsync_ExternalIdLocalAccount_UsesPreferredUsernameForEmail()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            AuthMeProvisioningService service = BuildService(dbContext);
            ClaimsPrincipal principal = BuildPrincipal(
                new Claim(ExternalIdentityClaims.EasyAuthProviderClaimType, "externalid"),
                new Claim("iss", "https://tenant.ciamlogin.com/tenant-id/v2.0"),
                new Claim("sub", "customer-456"),
                new Claim("preferred_username", "reader@gmail.com"),
                new Claim("name", "Reader Example"));
            string userId = "extid:https%3A%2F%2Ftenant.ciamlogin.com%2Ftenant-id%2Fv2.0:customer-456";

            AuthMeProvisioningResult result = await service.ProvisionAsync(principal, userId, CancellationToken.None);

            Assert.Equal("reader@gmail.com", result.ProfileIdentity.Email);
            UserProfile profile = await dbContext.UserProfiles.SingleAsync(item => item.UserId == userId);
            Assert.Equal("reader@gmail.com", profile.Email);
        }

        [Fact]
        public async Task ProvisionAsync_GoogleFederatedExternalId_UsesAlternateEmailClaim()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            AuthMeProvisioningService service = BuildService(dbContext);
            ClaimsPrincipal principal = BuildPrincipal(
                new Claim(ExternalIdentityClaims.EasyAuthProviderClaimType, "externalid"),
                new Claim("iss", "https://tenant.ciamlogin.com/tenant-id/v2.0"),
                new Claim("sub", "google-user-123"),
                new Claim("emails", "[\"reader@gmail.com\"]"),
                new Claim("name", "Johan"));
            string userId = "extid:https%3A%2F%2Ftenant.ciamlogin.com%2Ftenant-id%2Fv2.0:google-user-123";

            AuthMeProvisioningResult result = await service.ProvisionAsync(principal, userId, CancellationToken.None);

            Assert.Equal("reader@gmail.com", result.ProfileIdentity.Email);
            UserProfile profile = await dbContext.UserProfiles.SingleAsync(item => item.UserId == userId);
            Assert.Equal("reader@gmail.com", profile.Email);
            Assert.Equal("Johan", profile.DisplayName);
        }

        [Fact]
        public async Task ProvisionAsync_DuplicateEmailDetected_DoesNotCreateProfileOrEntitlement()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            dbContext.UserProfiles.Add(new UserProfile
            {
                UserId = "legacy-user-1",
                Email = "reader@gmail.com",
                DisplayName = "Existing Reader",
                CreatedUtc = DateTime.UtcNow.AddDays(-10),
                UpdatedUtc = DateTime.UtcNow.AddDays(-1),
                HasOnboarded = true
            });
            await dbContext.SaveChangesAsync();

            AuthMeProvisioningService service = BuildService(dbContext);
            ClaimsPrincipal principal = BuildPrincipal(
                new Claim(ExternalIdentityClaims.EasyAuthProviderClaimType, "externalid"),
                new Claim("iss", "https://tenant.ciamlogin.com/tenant-id/v2.0"),
                new Claim("sub", "customer-999"),
                new Claim("email", "reader@gmail.com"),
                new Claim("name", "Reader Example"));
            string userId = "extid:https%3A%2F%2Ftenant.ciamlogin.com%2Ftenant-id%2Fv2.0:customer-999";

            AuthMeProvisioningResult result = await service.ProvisionAsync(principal, userId, CancellationToken.None);

            Assert.Equal(AuthMeProvisioningStatus.DuplicateEmailDetected, result.Status);
            Assert.Null(result.Entitlement);
            Assert.False(result.CreatedProfile);
            Assert.False(result.CreatedEntitlement);
            Assert.NotNull(result.DuplicateEmail);
            Assert.Equal("externalid", result.DuplicateEmail!.CurrentLoginProvider);
            Assert.Equal(1, await dbContext.UserProfiles.CountAsync());
            Assert.Equal(0, await dbContext.UserEntitlements.CountAsync());
            Assert.Equal(0, await dbContext.ExternalIdentityLinks.CountAsync());
        }

        [Fact]
        public async Task ProvisionAsync_ExistingLogin_UpdatesExternalIdentityLinkLastSeen()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            DateTime firstSeen = DateTime.UtcNow.AddDays(-1);
            string userId = "legacy-user-1";
            dbContext.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Email = "legacy@example.com",
                DisplayName = "Legacy User",
                CreatedUtc = firstSeen,
                UpdatedUtc = firstSeen,
                HasOnboarded = true
            });
            dbContext.ExternalIdentityLinks.Add(new ExternalIdentityLink
            {
                UserId = userId,
                Provider = "aad",
                ObjectIdentifier = userId,
                EmailAtLinkTime = "legacy@example.com",
                CreatedUtc = firstSeen,
                LastSeenUtc = firstSeen
            });
            await dbContext.SaveChangesAsync();

            AuthMeProvisioningService service = BuildService(dbContext);
            ClaimsPrincipal principal = BuildPrincipal(
                new Claim(ExternalIdentityClaims.EasyAuthProviderClaimType, "aad"),
                new Claim("oid", userId),
                new Claim("email", "legacy@example.com"));

            AuthMeProvisioningResult result = await service.ProvisionAsync(principal, userId, CancellationToken.None);

            Assert.Equal(AuthMeProvisioningStatus.SuccessExisting, result.Status);
            ExternalIdentityLink link = await dbContext.ExternalIdentityLinks.SingleAsync(item => item.UserId == userId);
            Assert.True(link.LastSeenUtc >= firstSeen);
            Assert.Equal("aad", link.Provider);
        }

        [Fact]
        public async Task ProvisionAsync_ReturningExternalIdCustomer_DoesNotDuplicateRows()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            AuthMeProvisioningService service = BuildService(dbContext);
            ClaimsPrincipal principal = BuildPrincipal(
                new Claim("iss", "https://tenant.ciamlogin.com/tenant-id/v2.0/"),
                new Claim("sub", "customer-123"),
                new Claim("email", "reader@gmail.com"),
                new Claim("name", "Reader Example"));
            string userId = "extid:https%3A%2F%2Ftenant.ciamlogin.com%2Ftenant-id%2Fv2.0:customer-123";

            _ = await service.ProvisionAsync(principal, userId, CancellationToken.None);
            AuthMeProvisioningResult second = await service.ProvisionAsync(principal, userId, CancellationToken.None);

            Assert.False(second.CreatedProfile);
            Assert.False(second.CreatedEntitlement);
            Assert.Equal(1, await dbContext.UserProfiles.CountAsync(item => item.UserId == userId));
            Assert.Equal(1, await dbContext.UserEntitlements.CountAsync(item => item.UserId == userId));
        }

        [Fact]
        public async Task ProvisionAsync_LegacyWorkforceUser_StillUsesLegacyUserId()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            AuthMeProvisioningService service = BuildService(dbContext);
            ClaimsPrincipal principal = BuildPrincipal(
                new Claim("oid", "11111111-2222-3333-4444-555555555555"),
                new Claim("email", "person@contoso.com"),
                new Claim("name", "Contoso Person"));
            string userId = "11111111-2222-3333-4444-555555555555";

            AuthMeProvisioningResult result = await service.ProvisionAsync(principal, userId, CancellationToken.None);

            Assert.True(result.CreatedProfile);
            Assert.True(result.CreatedEntitlement);
            Assert.Equal(userId, result.ProfileIdentity.UserId);
            Assert.Equal(1, await dbContext.UserProfiles.CountAsync(item => item.UserId == userId));
            Assert.Equal(1, await dbContext.UserEntitlements.CountAsync(item => item.UserId == userId));
        }

        [Fact]
        public async Task ProvisionAsync_TombstonedUser_IsBlocked()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            DeletedUserIdentityService deletedUserIdentityService = new(dbContext);
            string userId = "extid:https%3A%2F%2Ftenant.ciamlogin.com%2Ftenant-id%2Fv2.0:customer-123";
            await deletedUserIdentityService.UpsertDeletedIdentityAsync(
                userId,
                "reader@gmail.com",
                "Reader Example",
                "admin-1",
                "admin@example.com",
                "deleted",
                CancellationToken.None);
            await dbContext.SaveChangesAsync();

            AuthMeProvisioningService service = new(
                dbContext,
                deletedUserIdentityService,
                BuildEntitlementStore(dbContext),
                NullLogger<AuthMeProvisioningService>.Instance);
            ClaimsPrincipal principal = BuildPrincipal(
                new Claim("iss", "https://tenant.ciamlogin.com/tenant-id/v2.0"),
                new Claim("sub", "customer-123"));

            await Assert.ThrowsAsync<DeletedUserIdentityException>(() =>
                service.ProvisionAsync(principal, userId, CancellationToken.None));

            Assert.Equal(0, await dbContext.UserProfiles.CountAsync(item => item.UserId == userId));
            Assert.Equal(0, await dbContext.UserEntitlements.CountAsync(item => item.UserId == userId));
        }

        [Fact]
        public async Task ProvisionAsync_RepeatedLegacyLogin_DoesNotCreateDuplicateEntitlements()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            AuthMeProvisioningService service = BuildService(dbContext);
            ClaimsPrincipal principal = BuildPrincipal(
                new Claim("oid", "legacy-user-1"),
                new Claim("email", "legacy@example.com"));

            _ = await service.ProvisionAsync(principal, "legacy-user-1", CancellationToken.None);
            _ = await service.ProvisionAsync(principal, "legacy-user-1", CancellationToken.None);
            _ = await service.ProvisionAsync(principal, "legacy-user-1", CancellationToken.None);

            Assert.Equal(1, await dbContext.UserEntitlements.CountAsync(item => item.UserId == "legacy-user-1"));
        }

        private static AppDbContext BuildDbContext(SqliteConnection connection)
        {
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            AppDbContext context = new(options);
            context.Database.EnsureCreated();
            return context;
        }

        private static AuthMeProvisioningService BuildService(AppDbContext dbContext)
        {
            return new AuthMeProvisioningService(
                dbContext,
                new DeletedUserIdentityService(dbContext),
                BuildEntitlementStore(dbContext),
                NullLogger<AuthMeProvisioningService>.Instance);
        }

        private static IUserEntitlementStore BuildEntitlementStore(AppDbContext dbContext)
        {
            return new UserEntitlementStore(
                dbContext,
                new TestClock(DateTime.UtcNow),
                new DeletedUserIdentityService(dbContext),
                NullLogger<UserEntitlementStore>.Instance);
        }

        private static ClaimsPrincipal BuildPrincipal(params Claim[] claims)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
        }

        private sealed class TestClock : IClock
        {
            private readonly DateTime _utcNow;

            public TestClock(DateTime utcNow)
            {
                _utcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
            }

            public DateTime UtcNow => _utcNow;
        }
    }
}
