using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Billing;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using WriterApp.Shared;

namespace WriterApp.Application.Users
{
    public sealed class AdminUsersService
    {
        private static readonly Regex EmailRegex = new(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly AppDbContext _dbContext;
        private readonly AdminPlanOverrideService _adminPlanOverrideService;
        private readonly AdminAuditService _adminAuditService;
        private readonly IUserEntitlementStore? _userEntitlementStore;
        private readonly IEntitlementService? _entitlementService;
        private readonly StripeApiClient? _stripeApiClient;
        private readonly StripeEntitlementSyncService? _stripeEntitlementSyncService;
        private readonly StripeOptions? _stripeOptions;
        private readonly ILogger<AdminUsersService> _logger;

        public AdminUsersService(
            AppDbContext dbContext,
            AdminPlanOverrideService adminPlanOverrideService,
            AdminAuditService adminAuditService,
            IUserEntitlementStore userEntitlementStore,
            IEntitlementService entitlementService,
            StripeApiClient stripeApiClient,
            StripeEntitlementSyncService stripeEntitlementSyncService,
            StripeOptions stripeOptions,
            ILogger<AdminUsersService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _adminPlanOverrideService = adminPlanOverrideService ?? throw new ArgumentNullException(nameof(adminPlanOverrideService));
            _adminAuditService = adminAuditService ?? throw new ArgumentNullException(nameof(adminAuditService));
            _userEntitlementStore = userEntitlementStore ?? throw new ArgumentNullException(nameof(userEntitlementStore));
            _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
            _stripeApiClient = stripeApiClient ?? throw new ArgumentNullException(nameof(stripeApiClient));
            _stripeEntitlementSyncService = stripeEntitlementSyncService ?? throw new ArgumentNullException(nameof(stripeEntitlementSyncService));
            _stripeOptions = stripeOptions ?? throw new ArgumentNullException(nameof(stripeOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public AdminUsersService(
            AppDbContext dbContext,
            AdminPlanOverrideService adminPlanOverrideService,
            ILogger<AdminUsersService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _adminPlanOverrideService = adminPlanOverrideService ?? throw new ArgumentNullException(nameof(adminPlanOverrideService));
            _adminAuditService = new AdminAuditService(dbContext);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AdminUserListResponseDto> QueryUsersAsync(
            int page,
            int pageSize,
            string? q,
            string? planKey,
            bool overrideOnly,
            string? subscriptionStatus,
            int? tokensLeftLt,
            int? tokensLeftGt,
            string? sort,
            CancellationToken ct = default)
        {
            int normalizedPage = page <= 0 ? 1 : page;
            int normalizedPageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

            IQueryable<AdminUserRow> query =
                from profile in _dbContext.UserProfiles.AsNoTracking()
                join entitlement in _dbContext.UserEntitlements.AsNoTracking()
                    on profile.UserId equals entitlement.UserId into entitlements
                from entitlement in entitlements.DefaultIfEmpty()
                select new AdminUserRow
                {
                    UserId = profile.UserId,
                    DisplayName = profile.DisplayName,
                    CreatedUtc = profile.CreatedUtc,
                    UpdatedUtc = profile.UpdatedUtc,
                    PlanKey = entitlement != null ? entitlement.PlanKey : null,
                    SubscriptionStatus = entitlement != null ? entitlement.SubscriptionStatus : null,
                    AiMonthlyTokenBudget = entitlement != null ? entitlement.AiMonthlyTokenBudget : 0,
                    AiTokensUsedThisPeriod = entitlement != null ? entitlement.AiTokensUsedThisPeriod : 0,
                    HasOverride = _dbContext.UserPlanAssignments.Any(assignment => assignment.UserId == profile.UserId)
                };

            if (!string.IsNullOrWhiteSpace(q))
            {
                string search = q.Trim();
                query = query.Where(item =>
                    item.UserId.Contains(search)
                    || (item.DisplayName != null && item.DisplayName.Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(planKey))
            {
                string normalized = UserEntitlementDefaults.NormalizePlanKey(planKey);
                query = normalized switch
                {
                    UserEntitlementDefaults.FreePlanKey =>
                        query.Where(item => item.PlanKey == null || item.PlanKey == "Free" || item.PlanKey == "free"),
                    UserEntitlementDefaults.StandardPlanKey =>
                        query.Where(item => item.PlanKey == "Standard" || item.PlanKey == "standard"),
                    _ =>
                        query.Where(item => item.PlanKey == "Professional" || item.PlanKey == "professional" || item.PlanKey == "Pro" || item.PlanKey == "pro")
                };
            }

            if (!string.IsNullOrWhiteSpace(subscriptionStatus))
            {
                string normalizedStatus = subscriptionStatus.Trim();
                query = query.Where(item => item.SubscriptionStatus != null && item.SubscriptionStatus == normalizedStatus);
            }

            if (overrideOnly)
            {
                query = query.Where(item => item.HasOverride);
            }

            if (tokensLeftLt.HasValue)
            {
                query = query.Where(item => (item.AiMonthlyTokenBudget - item.AiTokensUsedThisPeriod) < tokensLeftLt.Value);
            }

            if (tokensLeftGt.HasValue)
            {
                query = query.Where(item => (item.AiMonthlyTokenBudget - item.AiTokensUsedThisPeriod) > tokensLeftGt.Value);
            }

            query = ApplySort(query, sort);

            int totalCount = await query.CountAsync(ct);
            List<AdminUserRow> rows = await query
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToListAsync(ct);

            IReadOnlyList<AdminUserListItemDto> items = rows
                .Select(row => new AdminUserListItemDto(
                    row.UserId,
                    ResolveEmail(row.DisplayName, row.UserId),
                    row.DisplayName,
                    row.CreatedUtc,
                    row.UpdatedUtc,
                    UserEntitlementDefaults.NormalizePlanKey(row.PlanKey),
                    UserEntitlementDefaults.NormalizeSubscriptionStatus(row.SubscriptionStatus),
                    row.AiMonthlyTokenBudget,
                    row.AiTokensUsedThisPeriod,
                    Math.Max(0, row.AiMonthlyTokenBudget - row.AiTokensUsedThisPeriod),
                    row.HasOverride))
                .ToList();

            return new AdminUserListResponseDto(items, normalizedPage, normalizedPageSize, totalCount);
        }

        public async Task<AdminUserDetailDto?> GetUserAsync(string userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            string normalizedUserId = IdNorm.Norm(userId);
            UserProfile? profile = await _dbContext.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserId == normalizedUserId, ct);

            if (profile is null)
            {
                return null;
            }

            UserEntitlement? entitlement = await _dbContext.UserEntitlements
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserId == normalizedUserId, ct);

            UserPlanAssignment? latestOverride = await _dbContext.UserPlanAssignments
                .AsNoTracking()
                .Include(item => item.Plan)
                .Where(item => item.UserId == normalizedUserId)
                .OrderByDescending(item => item.AssignedUtc)
                .ThenByDescending(item => item.PlanId)
                .FirstOrDefaultAsync(ct);

            int budget = entitlement?.AiMonthlyTokenBudget ?? 0;
            int used = entitlement?.AiTokensUsedThisPeriod ?? 0;
            return new AdminUserDetailDto(
                profile.UserId,
                ResolveEmail(profile.DisplayName, profile.UserId),
                profile.DisplayName,
                profile.CreatedUtc,
                profile.UpdatedUtc,
                UserEntitlementDefaults.NormalizePlanKey(entitlement?.PlanKey),
                UserEntitlementDefaults.NormalizeSubscriptionStatus(entitlement?.SubscriptionStatus),
                budget,
                used,
                Math.Max(0, budget - used),
                latestOverride is not null,
                UserEntitlementDefaults.NormalizePlanKey(latestOverride?.Plan?.Key),
                latestOverride?.AssignedUtc,
                latestOverride?.AssignedBy,
                entitlement?.StripeCustomerId,
                entitlement?.StripeSubscriptionId);
        }

        public async Task<AdminUserDetailDto> CreateUserAsync(
            AdminCreateUserRequest request,
            string adminUserId,
            string? adminEmail,
            CancellationToken ct = default)
        {
            string requestedUserId = request.UserId?.Trim() ?? string.Empty;
            string userId = string.IsNullOrWhiteSpace(requestedUserId)
                ? Guid.NewGuid().ToString("D")
                : IdNorm.Norm(requestedUserId);
            string? displayName = request.DisplayName?.Trim();
            string? email = request.Email?.Trim();

            if (string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Either displayName or email is required.", nameof(request));
            }

            bool exists = await _dbContext.UserProfiles.AnyAsync(item => item.UserId == userId, ct);
            if (exists)
            {
                throw new InvalidOperationException($"User '{userId}' already exists.");
            }

            DateTime now = DateTime.UtcNow;

            // EasyAuth identities are still source-of-truth for authentication; this is only pre-provisioning metadata.
            UserProfile profile = new()
            {
                UserId = userId,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
                HasOnboarded = false,
                CreatedUtc = now,
                UpdatedUtc = now
            };

            _dbContext.UserProfiles.Add(profile);
            await _dbContext.SaveChangesAsync(ct);
            await _adminAuditService.WriteAsync(
                adminUserId,
                adminEmail,
                "CreateUser",
                profile.UserId,
                ResolveEmail(profile.DisplayName, profile.UserId),
                new
                {
                    request.Email,
                    request.DisplayName,
                    request.UserId
                },
                ct);

            AdminUserDetailDto? snapshot = await GetUserAsync(userId, ct);
            return snapshot ?? throw new InvalidOperationException("Failed to load created user.");
        }

        public async Task<AdminUserDetailDto?> UpdateUserAsync(
            string userId,
            AdminUpdateUserRequest request,
            string adminUserId,
            string? adminEmail,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            string normalizedUserId = IdNorm.Norm(userId);
            UserProfile? profile = await _dbContext.UserProfiles
                .FirstOrDefaultAsync(item => item.UserId == normalizedUserId, ct);

            if (profile is null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                throw new ArgumentException("Email edits are not supported because email is not stored as a dedicated column.");
            }

            string? nextDisplayName = request.DisplayName?.Trim();
            if (!string.Equals(profile.DisplayName, nextDisplayName, StringComparison.Ordinal))
            {
                profile.DisplayName = nextDisplayName;
                profile.UpdatedUtc = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(ct);
                await _adminAuditService.WriteAsync(
                    adminUserId,
                    adminEmail,
                    "UpdateUser",
                    profile.UserId,
                    ResolveEmail(profile.DisplayName, profile.UserId),
                    new
                    {
                        request.DisplayName
                    },
                    ct);
            }

            return await GetUserAsync(normalizedUserId, ct);
        }

        public async Task<bool> DeleteUserAsync(
            string userId,
            bool allowDeleteWithActiveSubscription,
            string adminUserId,
            string? adminEmail,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            string normalizedUserId = IdNorm.Norm(userId);
            UserProfile? profile = await _dbContext.UserProfiles
                .FirstOrDefaultAsync(item => item.UserId == normalizedUserId, ct);
            UserEntitlement? entitlement = await _dbContext.UserEntitlements
                .FirstOrDefaultAsync(item => item.UserId == normalizedUserId, ct);

            if (profile is null && entitlement is null)
            {
                return false;
            }

            if (!allowDeleteWithActiveSubscription && HasActiveSubscription(entitlement))
            {
                throw new InvalidOperationException("Cannot delete user with active subscription when Admin:AllowDeleteWithActiveSubscription is false.");
            }

            if (profile is not null)
            {
                profile.DisplayName = "Deleted user";
                profile.HasOnboarded = false;
                profile.UpdatedUtc = DateTime.UtcNow;
            }

            UserPlanAssignment[] assignments = await _dbContext.UserPlanAssignments
                .Where(item => item.UserId == normalizedUserId)
                .ToArrayAsync(ct);
            if (assignments.Length > 0)
            {
                _dbContext.UserPlanAssignments.RemoveRange(assignments);
            }

            if (entitlement is not null)
            {
                _dbContext.UserEntitlements.Remove(entitlement);
            }

            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Admin soft-delete applied for user {UserId}.", normalizedUserId);
            await _adminAuditService.WriteAsync(
                adminUserId,
                adminEmail,
                "SoftDeleteUser",
                normalizedUserId,
                ResolveEmail(profile?.DisplayName, normalizedUserId),
                new
                {
                    allowDeleteWithActiveSubscription
                },
                ct);
            return true;
        }

        public async Task<AdminPlanOverrideResponse> SetPlanOverrideAsync(
            string userId,
            AdminSetPlanOverrideRequest request,
            string adminCallerId,
            string? adminCallerEmail,
            CancellationToken ct = default)
        {
            AdminPlanOverrideResponse response = await _adminPlanOverrideService.SetOverride(
                userId,
                request.PlanKey,
                adminCallerId,
                adminCallerEmail,
                request.Reason,
                ct);
            await _adminAuditService.WriteAsync(
                adminCallerId,
                adminCallerEmail,
                string.IsNullOrWhiteSpace(request.PlanKey) ? "ClearPlanOverride" : "SetPlanOverride",
                response.UserId,
                ResolveEmail(null, response.UserId),
                new
                {
                    request.PlanKey,
                    request.Reason,
                    response.ResolvedPlanKey
                },
                ct);
            return response;
        }

        public async Task<AdminUserDetailDto> ResetOnboardingAsync(
            string userId,
            string adminUserId,
            string? adminEmail,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("userId is required.", nameof(userId));
            }

            string normalizedUserId = IdNorm.Norm(userId);
            DateTime now = DateTime.UtcNow;
            UserProfile? profile = await _dbContext.UserProfiles
                .FirstOrDefaultAsync(item => item.UserId == normalizedUserId, ct);

            if (profile is null)
            {
                profile = new UserProfile
                {
                    UserId = normalizedUserId,
                    DisplayName = ResolveEmail(null, normalizedUserId),
                    HasOnboarded = false,
                    CreatedUtc = now,
                    UpdatedUtc = now
                };
                _dbContext.UserProfiles.Add(profile);
            }

            profile.HasCompletedOnboarding = false;
            profile.OnboardingStep = 0;
            profile.OnboardingStartedUtc = null;
            profile.OnboardingCompletedUtc = null;
            profile.PrimaryWritingIntent = null;
            profile.UpdatedUtc = now;

            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Admin reset onboarding state. AdminUserId={AdminUserId} TargetUserId={TargetUserId}",
                adminUserId,
                normalizedUserId);

            await _adminAuditService.WriteAsync(
                adminUserId,
                adminEmail,
                "ResetOnboardingState",
                normalizedUserId,
                ResolveEmail(profile.DisplayName, normalizedUserId),
                new { reset = true },
                ct);

            AdminUserDetailDto? snapshot = await GetUserAsync(normalizedUserId, ct);
            return snapshot ?? throw new InvalidOperationException("Failed to load user after onboarding reset.");
        }

        public async Task<AdminTokenOperationResponse> ResetTokensPeriodAsync(
            string userId,
            string adminUserId,
            string? adminEmail,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("userId is required.", nameof(userId));
            }

            string normalizedUserId = IdNorm.Norm(userId);
            if (_userEntitlementStore is null || _entitlementService is null)
            {
                throw new InvalidOperationException("Token operations are not configured.");
            }
            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(normalizedUserId, ct);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            entitlement.AiTokensUsedThisPeriod = 0;
            entitlement.PeriodStartUtc = now;
            entitlement.UpdatedUtc = now;
            await _dbContext.SaveChangesAsync(ct);
            _entitlementService.InvalidateForUser(normalizedUserId);

            await _adminAuditService.WriteAsync(
                adminUserId,
                adminEmail,
                "ResetTokensPeriod",
                normalizedUserId,
                ResolveEmail(null, normalizedUserId),
                new { entitlement.PeriodStartUtc },
                ct);

            return new AdminTokenOperationResponse(
                normalizedUserId,
                entitlement.AiMonthlyTokenBudget,
                entitlement.AiTokensUsedThisPeriod,
                Math.Max(0, entitlement.AiMonthlyTokenBudget - entitlement.AiTokensUsedThisPeriod),
                entitlement.PeriodStartUtc);
        }

        public async Task<AdminTokenOperationResponse> AdjustTokensAsync(
            string userId,
            int deltaTokens,
            string reason,
            string adminUserId,
            string? adminEmail,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("userId is required.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("reason is required.", nameof(reason));
            }

            string normalizedUserId = IdNorm.Norm(userId);
            if (_userEntitlementStore is null || _entitlementService is null)
            {
                throw new InvalidOperationException("Token operations are not configured.");
            }
            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(normalizedUserId, ct);

            // Positive delta grants tokens by reducing used, negative delta consumes extra tokens.
            int nextUsed = entitlement.AiTokensUsedThisPeriod - deltaTokens;
            entitlement.AiTokensUsedThisPeriod = Math.Max(0, nextUsed);
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;

            _dbContext.TokenAdjustments.Add(new TokenAdjustment
            {
                UserId = normalizedUserId,
                DeltaTokens = deltaTokens,
                Reason = reason.Trim(),
                AdjustedBy = adminUserId,
                AdjustedByEmail = adminEmail,
                OccurredAtUtc = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(ct);
            _entitlementService.InvalidateForUser(normalizedUserId);

            await _adminAuditService.WriteAsync(
                adminUserId,
                adminEmail,
                "AdjustTokens",
                normalizedUserId,
                ResolveEmail(null, normalizedUserId),
                new
                {
                    deltaTokens,
                    reason,
                    entitlement.AiTokensUsedThisPeriod
                },
                ct);

            return new AdminTokenOperationResponse(
                normalizedUserId,
                entitlement.AiMonthlyTokenBudget,
                entitlement.AiTokensUsedThisPeriod,
                Math.Max(0, entitlement.AiMonthlyTokenBudget - entitlement.AiTokensUsedThisPeriod),
                entitlement.PeriodStartUtc);
        }

        public async Task<AdminUserDetailDto> SyncStripeForUserAsync(
            string userId,
            string adminUserId,
            string? adminEmail,
            CancellationToken ct = default)
        {
            if (_stripeOptions is null
                || _stripeApiClient is null
                || _stripeEntitlementSyncService is null
                || !_stripeOptions.Enabled
                || string.IsNullOrWhiteSpace(_stripeOptions.SecretKey))
            {
                throw new InvalidOperationException("Stripe integration is not configured.");
            }

            string normalizedUserId = IdNorm.Norm(userId);
            if (_userEntitlementStore is null)
            {
                throw new InvalidOperationException("Entitlement store is not configured.");
            }
            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(normalizedUserId, ct);

            string? customerId = entitlement.StripeCustomerId;
            string? subscriptionId = entitlement.StripeSubscriptionId;
            if (string.IsNullOrWhiteSpace(customerId))
            {
                customerId = await _stripeApiClient.FindCustomerByUserIdAsync(_stripeOptions.SecretKey, normalizedUserId, ct);
            }

            JsonDocument subscriptionDoc;
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                subscriptionDoc = await _stripeApiClient.GetSubscriptionAsync(_stripeOptions.SecretKey, subscriptionId, ct);
            }
            else if (!string.IsNullOrWhiteSpace(customerId))
            {
                using JsonDocument listDoc = await _stripeApiClient.ListSubscriptionsByCustomerAsync(_stripeOptions.SecretKey, customerId, ct);
                if (!listDoc.RootElement.TryGetProperty("data", out var data)
                    || data.ValueKind != System.Text.Json.JsonValueKind.Array
                    || data.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException("No Stripe subscription found for this user.");
                }

                subscriptionDoc = JsonDocument.Parse(data[0].GetRawText());
            }
            else
            {
                throw new InvalidOperationException("No Stripe customer found for this user.");
            }

            using (subscriptionDoc)
            {
                await _stripeEntitlementSyncService.SyncFromSubscriptionAsync(
                    normalizedUserId,
                    customerId,
                    subscriptionDoc.RootElement,
                    ct);
            }

            await _adminAuditService.WriteAsync(
                adminUserId,
                adminEmail,
                "StripeSyncUser",
                normalizedUserId,
                ResolveEmail(null, normalizedUserId),
                new
                {
                    customerId
                },
                ct);

            AdminUserDetailDto? snapshot = await GetUserAsync(normalizedUserId, ct);
            return snapshot ?? throw new InvalidOperationException("Failed to load user after Stripe sync.");
        }

        public async Task<string> ExportCsvAsync(
            int page,
            int pageSize,
            string? q,
            string? planKey,
            bool overrideOnly,
            string? subscriptionStatus,
            int? tokensLeftLt,
            int? tokensLeftGt,
            string? sort,
            string adminUserId,
            string? adminEmail,
            CancellationToken ct = default)
        {
            AdminUserListResponseDto response = await QueryUsersAsync(
                page <= 0 ? 1 : page,
                pageSize <= 0 ? 100 : Math.Min(pageSize, 1000),
                q,
                planKey,
                overrideOnly,
                subscriptionStatus,
                tokensLeftLt,
                tokensLeftGt,
                sort,
                ct);

            StringBuilder csv = new();
            csv.AppendLine("UserId,Email,DisplayName,PlanKey,SubscriptionStatus,AiMonthlyTokenBudget,AiTokensUsedThisPeriod,TokensLeft,IsManuallyOverridden,CreatedAtUtc,LastSeenUtc");
            foreach (AdminUserListItemDto item in response.Items)
            {
                csv.Append(EscapeCsv(item.UserId)).Append(',')
                    .Append(EscapeCsv(item.Email)).Append(',')
                    .Append(EscapeCsv(item.DisplayName)).Append(',')
                    .Append(EscapeCsv(item.PlanKey)).Append(',')
                    .Append(EscapeCsv(item.SubscriptionStatus)).Append(',')
                    .Append(item.AiMonthlyTokenBudget).Append(',')
                    .Append(item.AiTokensUsedThisPeriod).Append(',')
                    .Append(item.TokensLeft).Append(',')
                    .Append(item.IsManuallyOverridden ? "true" : "false").Append(',')
                    .Append(item.CreatedAtUtc.ToString("O")).Append(',')
                    .Append(item.LastSeenUtc.ToString("O"))
                    .AppendLine();
            }

            await _adminAuditService.WriteAsync(
                adminUserId,
                adminEmail,
                "ExportUsersCsv",
                null,
                null,
                new
                {
                    page,
                    pageSize,
                    q,
                    planKey,
                    subscriptionStatus
                },
                ct);

            return csv.ToString();
        }

        private static IQueryable<AdminUserRow> ApplySort(IQueryable<AdminUserRow> query, string? sort)
        {
            string normalized = (sort ?? "createdAt desc").Trim();
            string[] parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string field = parts.Length > 0 ? parts[0] : "createdAt";
            bool desc = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

            return field.ToLowerInvariant() switch
            {
                "email" => desc
                    ? query.OrderByDescending(item => item.DisplayName).ThenBy(item => item.UserId)
                    : query.OrderBy(item => item.DisplayName).ThenBy(item => item.UserId),
                "tokensleft" => desc
                    ? query.OrderByDescending(item => item.AiMonthlyTokenBudget - item.AiTokensUsedThisPeriod).ThenBy(item => item.UserId)
                    : query.OrderBy(item => item.AiMonthlyTokenBudget - item.AiTokensUsedThisPeriod).ThenBy(item => item.UserId),
                "lastseen" => desc
                    ? query.OrderByDescending(item => item.UpdatedUtc).ThenBy(item => item.UserId)
                    : query.OrderBy(item => item.UpdatedUtc).ThenBy(item => item.UserId),
                "userid" => desc
                    ? query.OrderByDescending(item => item.UserId)
                    : query.OrderBy(item => item.UserId),
                _ => desc
                    ? query.OrderByDescending(item => item.CreatedUtc).ThenBy(item => item.UserId)
                    : query.OrderBy(item => item.CreatedUtc).ThenBy(item => item.UserId)
            };
        }

        private static bool HasActiveSubscription(UserEntitlement? entitlement)
        {
            if (entitlement is null)
            {
                return false;
            }

            string status = UserEntitlementDefaults.NormalizeSubscriptionStatus(entitlement.SubscriptionStatus);
            if (status.Equals("canceled", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(entitlement.StripeSubscriptionId)
                   && (status.Equals("Active", StringComparison.OrdinalIgnoreCase)
                       || status.Equals("active", StringComparison.OrdinalIgnoreCase)
                       || status.Equals("trialing", StringComparison.OrdinalIgnoreCase)
                       || status.Equals("past_due", StringComparison.OrdinalIgnoreCase)
                       || status.Equals("incomplete", StringComparison.OrdinalIgnoreCase)
                       || status.Equals("unpaid", StringComparison.OrdinalIgnoreCase));
        }

        private static string? ResolveEmail(string? displayName, string userId)
        {
            if (!string.IsNullOrWhiteSpace(displayName) && EmailRegex.IsMatch(displayName))
            {
                return displayName;
            }

            if (!string.IsNullOrWhiteSpace(userId) && EmailRegex.IsMatch(userId))
            {
                return userId;
            }

            return null;
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
            return $"\"{escaped}\"";
        }

        private sealed class AdminUserRow
        {
            public string UserId { get; set; } = string.Empty;
            public string? DisplayName { get; set; }
            public DateTime CreatedUtc { get; set; }
            public DateTime UpdatedUtc { get; set; }
            public string? PlanKey { get; set; }
            public string? SubscriptionStatus { get; set; }
            public int AiMonthlyTokenBudget { get; set; }
            public int AiTokensUsedThisPeriod { get; set; }
            public bool HasOverride { get; set; }
        }
    }
}
