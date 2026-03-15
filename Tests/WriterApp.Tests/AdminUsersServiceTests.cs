using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Billing;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Application.Usage;
using WriterApp.Application.Users;
using WriterApp.Data;
using WriterApp.Data.AI;
using WriterApp.Data.Admin;
using WriterApp.Data.Documents;
using WriterApp.Data.Exporting;
using WriterApp.Data.Security;
using WriterApp.Data.Subscriptions;
using WriterApp.Data.Usage;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class AdminUsersServiceTests
    {
        [Fact]
        public void AdminApiFlag_DefaultsToDisabled()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            Assert.False(AdminPlanOverrideAccess.IsAdminApiEnabled(configuration));
        }

        [Fact]
        public void AdminApi_NonAdminPrincipal_IsBlocked()
        {
            ClaimsPrincipal principal = new(new ClaimsIdentity(
                new[] { new Claim("roles", "User") },
                authenticationType: "Test"));

            Assert.False(AdminPlanOverrideAccess.IsAuthorized(principal));
        }

        [Fact]
        public async Task QueryUsers_Paging_ReturnsTwentyAndTotalCount()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 25);

            AdminUsersService service = BuildService(dbContext);

            AdminUserListResponseDto response = await service.QueryUsersAsync(
                page: 1,
                pageSize: 20,
                q: null,
                planKey: null,
                overrideOnly: false,
                subscriptionStatus: null,
                tokensLeftLt: null,
                tokensLeftGt: null,
                sort: "createdAt asc");

            Assert.Equal(20, response.Items.Count);
            Assert.Equal(25, response.TotalCount);
            Assert.Equal(1, response.Page);
            Assert.Equal(20, response.PageSize);
        }

        [Fact]
        public async Task QueryUsers_Filters_WorkForPlanQueryAndOverrideOnly()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 6);

            AdminUsersService service = BuildService(dbContext);

            AdminUserListResponseDto response = await service.QueryUsersAsync(
                page: 1,
                pageSize: 20,
                q: "user-0",
                planKey: "Standard",
                overrideOnly: true,
                subscriptionStatus: null,
                tokensLeftLt: null,
                tokensLeftGt: null,
                sort: "createdAt asc");

            Assert.Single(response.Items);
            Assert.Equal("user-0", response.Items[0].UserId);
            Assert.Equal("Standard", response.Items[0].PlanKey);
            Assert.True(response.Items[0].IsManuallyOverridden);
        }

        [Fact]
        public async Task PlanOverride_UpdatesSnapshotImmediately()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 1);

            AdminUsersService service = BuildService(dbContext);

            AdminPlanOverrideResponse overrideResponse = await service.SetPlanOverrideAsync(
                "user-0",
                new AdminSetPlanOverrideRequest("Pro", "admin test"),
                "admin-1",
                "admin@example.com");

            AdminUserDetailDto? detail = await service.GetUserAsync("user-0");

            Assert.NotNull(detail);
            Assert.Equal(UserEntitlementDefaults.ProfessionalPlanKey, overrideResponse.ResolvedPlanKey);
            Assert.Equal(UserEntitlementDefaults.ProfessionalPlanKey, detail!.PlanKey);
            Assert.True(detail.IsManuallyOverridden);
        }

        [Fact]
        public async Task FindDuplicateCandidatesByEmail_ReturnsProfilesWithProviderHints()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            DateTime now = DateTime.UtcNow;
            dbContext.UserProfiles.AddRange(
                new UserProfile
                {
                    UserId = "legacy-user",
                    Email = "reader@example.com",
                    DisplayName = "Legacy Reader",
                    CreatedUtc = now.AddDays(-10),
                    UpdatedUtc = now.AddDays(-1),
                    HasOnboarded = true
                },
                new UserProfile
                {
                    UserId = "extid:https%3A%2F%2Ftenant.ciamlogin.com%2Ftenant%2Fv2.0:customer-1",
                    Email = "reader@example.com",
                    DisplayName = "Customer Reader",
                    CreatedUtc = now.AddDays(-2),
                    UpdatedUtc = now,
                    HasOnboarded = false
                });
            dbContext.UserEntitlements.AddRange(
                new UserEntitlement
                {
                    UserId = "legacy-user",
                    PlanKey = "Standard",
                    SubscriptionStatus = "active",
                    CreatedAt = now,
                    UpdatedUtc = now,
                    PeriodStartUtc = now
                },
                new UserEntitlement
                {
                    UserId = "extid:https%3A%2F%2Ftenant.ciamlogin.com%2Ftenant%2Fv2.0:customer-1",
                    PlanKey = "Free",
                    SubscriptionStatus = "active",
                    CreatedAt = now,
                    UpdatedUtc = now,
                    PeriodStartUtc = now
                });
            dbContext.ExternalIdentityLinks.AddRange(
                new ExternalIdentityLink
                {
                    UserId = "legacy-user",
                    Provider = "aad",
                    ObjectIdentifier = "legacy-user",
                    CreatedUtc = now,
                    LastSeenUtc = now
                },
                new ExternalIdentityLink
                {
                    UserId = "extid:https%3A%2F%2Ftenant.ciamlogin.com%2Ftenant%2Fv2.0:customer-1",
                    Provider = "externalid",
                    Issuer = "https://tenant.ciamlogin.com/tenant/v2.0",
                    Subject = "customer-1",
                    CreatedUtc = now,
                    LastSeenUtc = now
                });
            await dbContext.SaveChangesAsync();

            AdminUsersService service = BuildService(dbContext);

            AdminDuplicateAccountLookupResponseDto response = await service.FindDuplicateCandidatesByEmailAsync("reader@example.com");

            Assert.Equal("reader@example.com", response.Email);
            Assert.Equal(2, response.Matches.Count);
            Assert.Contains(response.Matches, item => item.UserId == "legacy-user" && item.ProviderHints.Contains("aad"));
            Assert.Contains(response.Matches, item => item.UserId.Contains("extid:", StringComparison.Ordinal) && item.ProviderHints.Contains("externalid"));
        }

        [Fact]
        public async Task PlanOverride_Succeeds_WhenAuditTableMissing()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 1);

            // Simulate schema drift where audit table was not created yet.
            dbContext.Database.ExecuteSqlRaw("""DROP TABLE IF EXISTS "AdminAuditEvents";""");

            AdminUsersService service = BuildService(dbContext);

            AdminPlanOverrideResponse response = await service.SetPlanOverrideAsync(
                "user-0",
                new AdminSetPlanOverrideRequest("Pro", "missing audit table"),
                "admin-1",
                "admin@example.com");

            AdminUserDetailDto? detail = await service.GetUserAsync("user-0");

            Assert.Equal(UserEntitlementDefaults.ProfessionalPlanKey, response.ResolvedPlanKey);
            Assert.NotNull(detail);
            Assert.True(detail!.IsManuallyOverridden);
            Assert.Equal(UserEntitlementDefaults.ProfessionalPlanKey, detail.PlanKey);
        }

        [Fact]
        public async Task AdminAuditWrite_CanceledToken_BubblesOperationCanceledException()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            AdminAuditService auditService = new(dbContext, NullLogger<AdminAuditService>.Instance);
            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                auditService.WriteAsync(
                    "admin-1",
                    "admin@example.com",
                    "SetPlanOverride",
                    "user-1",
                    "user@example.com",
                    new { reason = "cancelled" },
                    cts.Token));
        }

        [Fact]
        public async Task ResetToFirstRun_RemovesOwnedAppState_AndResetsProfile()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 1);
            SeedOwnedAppState(dbContext, "user-0");

            AdminUsersService service = BuildService(dbContext);

            AdminResetToFirstRunResponse response = await service.ResetToFirstRunAsync(
                "user-0",
                "admin-1",
                "admin@example.com");

            UserProfile profile = await dbContext.UserProfiles.FirstAsync(item => item.UserId == "user-0");
            UserEntitlement entitlement = await dbContext.UserEntitlements.FirstAsync(item => item.UserId == "user-0");

            Assert.Equal(1, response.DeletedProjects);
            Assert.Equal(1, response.DeletedOutlineTemplates);
            Assert.Equal(1, response.DeletedExportTemplates);
            Assert.Equal(1, response.DeletedExportPresets);
            Assert.Equal(1, response.DeletedPromptPresets);
            Assert.Equal(1, response.DeletedUsageEvents);
            Assert.Equal(1, response.DeletedUsageAggregates);
            Assert.Equal(1, response.DeletedUserEvents);
            Assert.True(response.ExternalIdentityPreserved);
            Assert.True(response.ResetToFreePlan);
            Assert.False(profile.HasOnboarded);
            Assert.False(profile.HasCompletedOnboarding);
            Assert.Equal(0, profile.OnboardingStep);
            Assert.Null(profile.OnboardingStartedUtc);
            Assert.Null(profile.OnboardingCompletedUtc);
            Assert.Null(profile.PrimaryWritingIntent);
            Assert.Equal(UserEntitlementDefaults.FreePlanKey, entitlement.PlanKey);
            Assert.Equal(0, entitlement.AiMonthlyTokenBudget);
            Assert.Equal(0, entitlement.AiTokensUsedThisPeriod);
            Assert.Empty(dbContext.Projects.Where(item => item.OwnerUserId == "user-0"));
            Assert.Empty(dbContext.OutlineTemplates.Where(item => item.OwnerUserId == "user-0"));
            Assert.Empty(dbContext.ExportTemplates.Where(item => item.OwnerUserId == "user-0"));
            Assert.Empty(dbContext.ExportPresets.Where(item => item.OwnerUserId == "user-0"));
            Assert.Empty(dbContext.PromptPresets.Where(item => item.OwnerUserId == "user-0"));
            Assert.Empty(dbContext.UsageEvents.Where(item => item.UserId == "user-0"));
            Assert.Empty(dbContext.UsageAggregates.Where(item => item.UserId == "user-0"));
            Assert.Empty(dbContext.UserEvents.Where(item => item.UserId == "user-0"));
            Assert.Empty(dbContext.UserPlanAssignments.Where(item => item.UserId == "user-0"));
        }

        [Fact]
        public async Task ResetToFirstRun_HandlesMissingRows_Gracefully()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            dbContext.UserProfiles.Add(new UserProfile
            {
                UserId = "partial-user",
                Email = "partial@example.com",
                DisplayName = "Partial User",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                HasOnboarded = true,
                HasCompletedOnboarding = true,
                OnboardingStep = 7,
                PrimaryWritingIntent = "Novel"
            });
            await dbContext.SaveChangesAsync();

            AdminUsersService service = BuildService(dbContext);

            AdminResetToFirstRunResponse response = await service.ResetToFirstRunAsync(
                "partial-user",
                "admin-1",
                "admin@example.com");

            Assert.Equal("partial-user", response.UserId);
            Assert.Equal(0, response.DeletedProjects);
            Assert.Equal(UserEntitlementDefaults.FreePlanKey, response.User.PlanKey);
            Assert.True(response.ExternalIdentityPreserved);
            Assert.NotNull(await dbContext.UserProfiles.FirstOrDefaultAsync(item => item.UserId == "partial-user"));
        }

        [Fact]
        public async Task ResetToFirstRun_PaidUser_IsReturnedToFree_AndPreservesStripeCustomer()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 2);

            UserEntitlement entitlement = await dbContext.UserEntitlements.FirstAsync(item => item.UserId == "user-1");
            entitlement.PlanKey = UserEntitlementDefaults.ProfessionalPlanKey;
            entitlement.AiMonthlyTokenBudget = UserEntitlementDefaults.ProfessionalMonthlyTokenBudget;
            entitlement.StripeCustomerId = "cus_123";
            entitlement.StripeSubscriptionId = "sub_123";
            entitlement.StripePriceId = "price_pro";
            entitlement.CurrentPeriodEndUtc = DateTimeOffset.UtcNow.AddDays(14);
            entitlement.CancelAtPeriodEnd = true;
            await dbContext.SaveChangesAsync();

            AdminUsersService service = BuildService(dbContext);

            AdminResetToFirstRunResponse response = await service.ResetToFirstRunAsync(
                "user-1",
                "admin-1",
                "admin@example.com");

            UserEntitlement updated = await dbContext.UserEntitlements.FirstAsync(item => item.UserId == "user-1");
            Assert.Equal(UserEntitlementDefaults.FreePlanKey, response.User.PlanKey);
            Assert.Equal(UserEntitlementDefaults.FreePlanKey, updated.PlanKey);
            Assert.Equal("cus_123", updated.StripeCustomerId);
            Assert.Null(updated.StripeSubscriptionId);
            Assert.Null(updated.StripePriceId);
            Assert.Null(updated.CurrentPeriodEndUtc);
            Assert.False(updated.CancelAtPeriodEnd);
            Assert.Empty(dbContext.UserPlanAssignments.Where(item => item.UserId == "user-1"));
        }

        [Fact]
        public async Task ResetToFirstRun_IsIdempotent_WhenCalledTwice()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 1);
            SeedOwnedAppState(dbContext, "user-0");

            AdminUsersService service = BuildService(dbContext);

            AdminResetToFirstRunResponse first = await service.ResetToFirstRunAsync("user-0", "admin-1", "admin@example.com");
            AdminResetToFirstRunResponse second = await service.ResetToFirstRunAsync("user-0", "admin-1", "admin@example.com");

            Assert.Equal(1, first.DeletedProjects);
            Assert.Equal(0, second.DeletedProjects);
            Assert.Equal(UserEntitlementDefaults.FreePlanKey, second.User.PlanKey);
            Assert.True(second.ExternalIdentityPreserved);
        }

        [Fact]
        public async Task ResetToFirstRun_PreservesExternalIdentityMapping()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 1);

            UserProfile before = await dbContext.UserProfiles.FirstAsync(item => item.UserId == "user-0");
            string? beforeEmail = before.Email;

            AdminUsersService service = BuildService(dbContext);

            _ = await service.ResetToFirstRunAsync("user-0", "admin-1", "admin@example.com");

            UserProfile after = await dbContext.UserProfiles.FirstAsync(item => item.UserId == "user-0");
            Assert.Equal("user-0", after.UserId);
            Assert.Equal(beforeEmail, after.Email);
        }

        [Fact]
        public async Task DeleteUser_RemovesApplicationState_ForNormalUser()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 1);

            AdminUsersService service = BuildService(dbContext);

            AdminDeleteCustomerResponse response = await service.DeleteUserAsync(
                "user-0",
                allowDeleteWithActiveSubscription: true,
                "admin-1",
                "admin@example.com");

            Assert.False(response.AlreadyDeleted);
            Assert.True(response.DeletedUserProfile);
            Assert.True(response.DeletedEntitlement);
            Assert.True(response.ExternalIdentityPreserved);
            Assert.True(response.PreservedAuditTrail);
            Assert.Null(await dbContext.UserProfiles.FirstOrDefaultAsync(item => item.UserId == "user-0"));
            Assert.Null(await dbContext.UserEntitlements.FirstOrDefaultAsync(item => item.UserId == "user-0"));
        }

        [Fact]
        public async Task DeleteUser_RemovesProjectsAndWorkspaceContent()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 1);
            SeedOwnedAppState(dbContext, "user-0");

            AdminUsersService service = BuildService(dbContext);

            AdminDeleteCustomerResponse response = await service.DeleteUserAsync(
                "user-0",
                allowDeleteWithActiveSubscription: true,
                "admin-1",
                "admin@example.com");

            Assert.Equal(1, response.DeletedProjects);
            Assert.Empty(dbContext.Projects.Where(item => item.OwnerUserId == "user-0"));
            Assert.Empty(dbContext.OutlineTemplates.Where(item => item.OwnerUserId == "user-0"));
            Assert.Empty(dbContext.ExportTemplates.Where(item => item.OwnerUserId == "user-0"));
            Assert.Empty(dbContext.ExportPresets.Where(item => item.OwnerUserId == "user-0"));
            Assert.Empty(dbContext.PromptPresets.Where(item => item.OwnerUserId == "user-0"));
        }

        [Fact]
        public async Task DeleteUser_RemovesEntitlementUsageAndOverrides()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 1);
            SeedOwnedAppState(dbContext, "user-0");

            dbContext.TokenAdjustments.Add(new TokenAdjustment
            {
                UserId = "user-0",
                DeltaTokens = 10,
                Reason = "test",
                AdjustedBy = "admin-1",
                OccurredAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();

            AdminUsersService service = BuildService(dbContext);

            AdminDeleteCustomerResponse response = await service.DeleteUserAsync(
                "user-0",
                allowDeleteWithActiveSubscription: true,
                "admin-1",
                "admin@example.com");

            Assert.True(response.DeletedEntitlement);
            Assert.True(response.RemovedPlanOverrides > 0);
            Assert.Equal(1, response.DeletedUsageEvents);
            Assert.Equal(1, response.DeletedUsageAggregates);
            Assert.Equal(1, response.DeletedUserEvents);
            Assert.Equal(1, response.DeletedTokenAdjustments);
            Assert.Empty(dbContext.UserPlanAssignments.Where(item => item.UserId == "user-0"));
            Assert.Empty(dbContext.TokenAdjustments.Where(item => item.UserId == "user-0"));
        }

        [Fact]
        public async Task DeleteUser_IsGraceful_WhenRepeated()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 1);

            AdminUsersService service = BuildService(dbContext);

            AdminDeleteCustomerResponse first = await service.DeleteUserAsync(
                "user-0",
                allowDeleteWithActiveSubscription: true,
                "admin-1",
                "admin@example.com");
            AdminDeleteCustomerResponse second = await service.DeleteUserAsync(
                "user-0",
                allowDeleteWithActiveSubscription: true,
                "admin-1",
                "admin@example.com");

            Assert.False(first.AlreadyDeleted);
            Assert.True(second.AlreadyDeleted);
        }

        [Fact]
        public async Task DeleteUser_PreservesAuditTrail_AndDoesNotTouchExternalIdentityProvider()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 1);

            dbContext.AdminAuditEvents.Add(new AdminAuditEvent
            {
                AdminUserId = "admin-seed",
                Action = "Seed",
                OccurredAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();

            AdminUsersService service = BuildService(dbContext);

            AdminDeleteCustomerResponse response = await service.DeleteUserAsync(
                "user-0",
                allowDeleteWithActiveSubscription: true,
                "admin-1",
                "admin@example.com");

            Assert.True(response.ExternalIdentityPreserved);
            Assert.True(response.PreservedAuditTrail);
            Assert.NotEmpty(dbContext.AdminAuditEvents);
        }

        [Fact]
        public async Task DeleteUser_WritesDeletedIdentityTombstone_AndBlocksEntitlementReprovision()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 1);

            AdminUsersService service = BuildService(dbContext);

            _ = await service.DeleteUserAsync(
                "user-0",
                allowDeleteWithActiveSubscription: true,
                "admin-1",
                "admin@example.com");

            DeletedUserIdentity? tombstone = await dbContext.DeletedUserIdentities.FirstOrDefaultAsync(item => item.UserId == "user-0");
            Assert.NotNull(tombstone);

            IUserEntitlementStore entitlementStore = new UserEntitlementStore(
                dbContext,
                new FixedClock(DateTime.UtcNow),
                new DeletedUserIdentityService(dbContext),
                NullLogger<UserEntitlementStore>.Instance);

            await Assert.ThrowsAsync<DeletedUserIdentityException>(() =>
                entitlementStore.GetOrCreateAsync("user-0", CancellationToken.None));
        }

        [Fact]
        public async Task GrantAdmin_AssignsManagedRole_AndListReflectsRoleAccess()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 1);

            AdminUsersService service = BuildService(dbContext);

            AdminRoleChangeResponse response = await service.GrantAdminAsync(
                "user-0",
                "bootstrap-admin",
                "admin@example.com");
            AdminUserListResponseDto list = await service.QueryUsersAsync(1, 20, null, null, false, null, null, null, null);
            IAdminAccessResolver resolver = new AdminAccessResolver(
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
                dbContext);
            ClaimsPrincipal promotedPrincipal = new(new ClaimsIdentity(new[] { new Claim("oid", "user-0") }, "Test"));

            Assert.Equal("grant_admin", response.Action);
            Assert.True(response.User.IsAdminAccess);
            Assert.Equal("Role", response.User.AdminAccessSource);
            Assert.True(response.User.HasRoleAdminAssignment);
            Assert.Contains(list.Items, item => item.UserId == "user-0" && item.AdminAccessSource == "Role" && item.HasRoleAdminAssignment);
            Assert.True(resolver.Resolve(promotedPrincipal).IsAdminAccess);
        }

        [Fact]
        public async Task RevokeAdmin_RemovesManagedRole()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 2);
            dbContext.AdminRoleAssignments.AddRange(
                new AdminRoleAssignment { UserId = "user-0", AssignedByUserId = "seed", AssignedUtc = DateTime.UtcNow },
                new AdminRoleAssignment { UserId = "user-1", AssignedByUserId = "seed", AssignedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();

            AdminUsersService service = BuildService(dbContext);

            AdminRoleChangeResponse response = await service.RevokeAdminAsync(
                "user-1",
                "user-0",
                "admin@example.com");

            Assert.Equal("revoke_admin", response.Action);
            Assert.False(response.User.IsAdminAccess);
            Assert.Equal("None", response.User.AdminAccessSource);
            Assert.False(response.User.HasRoleAdminAssignment);
            Assert.DoesNotContain(dbContext.AdminRoleAssignments, item => item.UserId == "user-1");
        }

        [Fact]
        public async Task RevokeAdmin_CannotSelfRevoke()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 1);
            dbContext.AdminRoleAssignments.Add(new AdminRoleAssignment { UserId = "user-0", AssignedByUserId = "seed", AssignedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();

            AdminUsersService service = BuildService(dbContext);

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RevokeAdminAsync("user-0", "user-0", "admin@example.com"));

            Assert.Equal("You cannot remove your own admin role.", ex.Message);
        }

        [Fact]
        public async Task AdminRoleChange_WritesAuditEvent()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 2);
            dbContext.AdminRoleAssignments.AddRange(
                new AdminRoleAssignment { UserId = "user-0", AssignedByUserId = "seed", AssignedUtc = DateTime.UtcNow },
                new AdminRoleAssignment { UserId = "user-1", AssignedByUserId = "seed", AssignedUtc = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();

            AdminUsersService service = BuildService(dbContext);

            _ = await service.GrantAdminAsync("user-0", "user-1", "admin@example.com");
            _ = await service.RevokeAdminAsync("user-1", "user-0", "admin@example.com");

            Assert.Contains(dbContext.AdminAuditEvents, item => item.Action == "grant_admin" && item.TargetUserId == "user-0");
            Assert.Contains(dbContext.AdminAuditEvents, item => item.Action == "revoke_admin" && item.TargetUserId == "user-1");
        }

        [Fact]
        public async Task ProjectDeletion_StandalonePath_StillDeletesProject()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            Guid projectId = SeedProject(dbContext, "user-standalone");
            ProjectDeletionService service = new(dbContext, NullLogger<ProjectDeletionService>.Instance);

            ProjectDeletionResult result = await service.DeleteOwnedProjectAsync(projectId, "user-standalone", CancellationToken.None);

            Assert.True(result.Deleted);
            Assert.Null(await dbContext.Projects.FirstOrDefaultAsync(item => item.Id == projectId));
        }

        [Fact]
        public async Task ProjectDeletion_ParticipatingPath_WorksInsideOuterTransaction()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            Guid projectId = SeedProject(dbContext, "user-participating");
            ProjectDeletionService service = new(dbContext, NullLogger<ProjectDeletionService>.Instance);

            await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync();
            ProjectDeletionResult result = await service.DeleteOwnedProjectInExistingTransactionAsync(projectId, "user-participating", CancellationToken.None);
            await transaction.CommitAsync();

            Assert.True(result.Deleted);
            Assert.Null(await dbContext.Projects.FirstOrDefaultAsync(item => item.Id == projectId));
        }

        [Fact]
        public async Task ResetToFirstRun_RollsBackOuterTransaction_WhenParticipatingDeletionFails()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 1);
            Guid projectId = SeedProject(dbContext, "user-0");

            ThrowingProjectDeletionService failingDeletion = new(dbContext, NullLogger<ProjectDeletionService>.Instance);
            AdminUsersService service = BuildService(dbContext, failingDeletion);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ResetToFirstRunAsync("user-0", "admin-1", "admin@example.com"));

            Assert.NotNull(await dbContext.Projects.FirstOrDefaultAsync(item => item.Id == projectId));
            Assert.NotNull(await dbContext.UserProfiles.FirstOrDefaultAsync(item => item.UserId == "user-0"));
        }

        [Fact]
        public async Task DeleteUser_RollsBackOuterTransaction_WhenParticipatingDeletionFails()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);
            SeedUsers(dbContext, count: 1);
            Guid projectId = SeedProject(dbContext, "user-0");

            ThrowingProjectDeletionService failingDeletion = new(dbContext, NullLogger<ProjectDeletionService>.Instance);
            AdminUsersService service = BuildService(dbContext, failingDeletion);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DeleteUserAsync("user-0", allowDeleteWithActiveSubscription: true, "admin-1", "admin@example.com"));

            Assert.NotNull(await dbContext.Projects.FirstOrDefaultAsync(item => item.Id == projectId));
            Assert.NotNull(await dbContext.UserProfiles.FirstOrDefaultAsync(item => item.UserId == "user-0"));
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

        private static AdminUsersService BuildService(AppDbContext dbContext, IProjectDeletionService? projectDeletionService = null)
        {
            AdminAuditService auditService = new(dbContext, NullLogger<AdminAuditService>.Instance);
            DeletedUserIdentityService deletedUserIdentityService = new(dbContext);
            IUserEntitlementStore entitlementStore = new UserEntitlementStore(
                dbContext,
                new FixedClock(DateTime.UtcNow),
                deletedUserIdentityService,
                NullLogger<UserEntitlementStore>.Instance);
            IEntitlementService entitlementService = new EntitlementService(
                new PlanRepository(dbContext),
                entitlementStore,
                new MemoryCache(new MemoryCacheOptions()));
            AdminPlanOverrideService overrideService = new(
                dbContext,
                entitlementStore,
                entitlementService,
                new StubStripePriceResolver(),
                NullLogger<AdminPlanOverrideService>.Instance);
            projectDeletionService ??= new ProjectDeletionService(
                dbContext,
                NullLogger<ProjectDeletionService>.Instance);
            IAdminAccessResolver adminAccessResolver = new AdminAccessResolver(
                new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>())
                    .Build(),
                dbContext);

            return new AdminUsersService(
                dbContext,
                overrideService,
                auditService,
                adminAccessResolver,
                deletedUserIdentityService,
                entitlementStore,
                entitlementService,
                projectDeletionService,
                NullLogger<AdminUsersService>.Instance);
        }

        private static Guid SeedProject(AppDbContext dbContext, string userId)
        {
            Guid projectId = Guid.NewGuid();
            dbContext.Projects.Add(new ProjectRecord
            {
                Id = projectId,
                OwnerUserId = userId,
                Title = "Project",
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow
            });
            dbContext.SaveChanges();
            return projectId;
        }

        private static void SeedUsers(AppDbContext dbContext, int count)
        {
            DateTime now = DateTime.UtcNow;
            Plan standardPlan = dbContext.Plans.First(plan => plan.Key == "standard");
            Plan professionalPlan = dbContext.Plans.First(plan => plan.Key == "professional");

            for (int i = 0; i < count; i++)
            {
                string userId = $"user-{i}";
                dbContext.UserProfiles.Add(new UserProfile
                {
                    UserId = userId,
                    DisplayName = $"user{i}@example.com",
                    CreatedUtc = now.AddMinutes(-i),
                    UpdatedUtc = now.AddMinutes(-i),
                    HasOnboarded = true
                });

                bool isStandard = i % 2 == 0;
                dbContext.UserEntitlements.Add(new UserEntitlement
                {
                    UserId = userId,
                    PlanKey = isStandard ? UserEntitlementDefaults.StandardPlanKey : UserEntitlementDefaults.ProfessionalPlanKey,
                    SubscriptionStatus = UserEntitlementDefaults.ActiveSubscriptionStatus,
                    CreatedAt = now.AddMinutes(-i),
                    AiMonthlyTokenBudget = isStandard
                        ? UserEntitlementDefaults.StandardMonthlyTokenBudget
                        : UserEntitlementDefaults.ProfessionalMonthlyTokenBudget,
                    AiTokensUsedThisPeriod = 1000,
                    PeriodStartUtc = now.AddDays(-2),
                    UpdatedUtc = now.AddMinutes(-i)
                });

                if (isStandard)
                {
                    dbContext.UserPlanAssignments.Add(new UserPlanAssignment
                    {
                        UserId = userId,
                        PlanId = standardPlan.PlanId,
                        AssignedUtc = now.AddMinutes(-i),
                        AssignedBy = "seed"
                    });
                }
                else
                {
                    dbContext.UserPlanAssignments.Add(new UserPlanAssignment
                    {
                        UserId = userId,
                        PlanId = professionalPlan.PlanId,
                        AssignedUtc = now.AddMinutes(-i),
                        AssignedBy = "seed"
                    });
                }
            }

            dbContext.SaveChanges();
        }

        private static void SeedOwnedAppState(AppDbContext dbContext, string userId)
        {
            DateTime now = DateTime.UtcNow;
            DateTimeOffset nowOffset = DateTimeOffset.UtcNow;
            Guid projectId = Guid.NewGuid();

            dbContext.Projects.Add(new ProjectRecord
            {
                Id = projectId,
                OwnerUserId = userId,
                Title = "Starter project",
                CreatedUtc = nowOffset,
                UpdatedUtc = nowOffset
            });

            UserProfile? profile = dbContext.UserProfiles.FirstOrDefault(item => item.UserId == userId);
            if (profile is not null)
            {
                profile.HasOnboarded = true;
                profile.HasCompletedOnboarding = true;
                profile.OnboardingStep = 10;
                profile.OnboardingStartedUtc = nowOffset.AddDays(-2);
                profile.OnboardingCompletedUtc = nowOffset.AddDays(-1);
                profile.PrimaryWritingIntent = "Novel";
                profile.UpdatedUtc = now;
            }

            dbContext.OutlineTemplates.Add(new OutlineTemplateRecord
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Name = "Outline",
                TemplateJson = "{}",
                CreatedUtc = nowOffset,
                UpdatedUtc = nowOffset
            });

            dbContext.ExportTemplates.Add(new ExportTemplate
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Name = "Manuscript",
                PageWidthMm = 148,
                PageHeightMm = 210,
                MarginTopMm = 20,
                MarginRightMm = 20,
                MarginBottomMm = 20,
                MarginLeftMm = 20,
                BodyFontSizePt = 12,
                LineHeight = 1.5m,
                ParagraphSpacingPt = 6,
                CreatedAt = nowOffset,
                UpdatedAt = nowOffset
            });

            dbContext.ExportPresets.Add(new ExportPreset
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Name = "Default",
                SettingsJson = "{}",
                CreatedAt = nowOffset,
                UpdatedAt = nowOffset
            });

            dbContext.PromptPresets.Add(new PromptPresetRecord
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                ProjectId = null,
                Name = "Preset",
                ParametersJson = "{}",
                CreatedUtc = nowOffset,
                UpdatedUtc = nowOffset
            });

            dbContext.UsageEvents.Add(new UsageEvent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Kind = "ai.total",
                Provider = "test",
                Model = "gpt-test",
                InputTokens = 10,
                OutputTokens = 5,
                TimestampUtc = now
            });

            dbContext.UsageAggregates.Add(new UsageAggregate
            {
                UserId = userId,
                Kind = "ai.total",
                PeriodStartUtc = now.Date,
                PeriodEndUtc = now.Date.AddMonths(1),
                TotalInputTokens = 10,
                TotalOutputTokens = 5,
                UpdatedUtc = now
            });

            dbContext.UserEvents.Add(new UserEvent
            {
                UserId = userId,
                EventName = "onboarding_started",
                CreatedUtc = nowOffset
            });

            dbContext.SaveChanges();
        }

        private sealed class StubStripePriceResolver : IStripePriceResolver
        {
            public string ResolvePriceId(string planKey, out string normalizedPlanKey)
            {
                normalizedPlanKey = UserEntitlementDefaults.NormalizePlanKey(planKey);
                return normalizedPlanKey switch
                {
                    UserEntitlementDefaults.StandardPlanKey => "price_standard",
                    UserEntitlementDefaults.ProfessionalPlanKey => "price_pro",
                    _ => "price_free"
                };
            }

            public string? ResolvePlanKey(string priceId)
            {
                return priceId switch
                {
                    "price_standard" => "standard",
                    "price_pro" => "pro",
                    _ => null
                };
            }
        }

        private sealed class FixedClock : IClock
        {
            public FixedClock(DateTime utcNow)
            {
                UtcNow = utcNow;
            }

            public DateTime UtcNow { get; }
        }

        private sealed class ThrowingProjectDeletionService : IProjectDeletionService
        {
            private readonly ProjectDeletionService _inner;

            public ThrowingProjectDeletionService(AppDbContext dbContext, Microsoft.Extensions.Logging.ILogger<ProjectDeletionService> logger)
            {
                _inner = new ProjectDeletionService(dbContext, logger);
            }

            public Task<ProjectDeletionResult> DeleteOwnedProjectAsync(Guid incomingId, string ownerUserId, CancellationToken ct)
                => _inner.DeleteOwnedProjectAsync(incomingId, ownerUserId, ct);

            public async Task<ProjectDeletionResult> DeleteOwnedProjectInExistingTransactionAsync(Guid incomingId, string ownerUserId, CancellationToken ct)
            {
                ProjectDeletionResult result = await _inner.DeleteOwnedProjectInExistingTransactionAsync(incomingId, ownerUserId, ct);
                throw new InvalidOperationException("Simulated failure after participating project deletion.");
            }
        }
    }
}
