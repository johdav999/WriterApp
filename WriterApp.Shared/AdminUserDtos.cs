using System;
using System.Collections.Generic;

namespace WriterApp.Shared
{
    public sealed record AdminUserQueryDto(
        int Page = 1,
        int PageSize = 20,
        string? Q = null,
        string? PlanKey = null,
        string? SubscriptionStatus = null,
        bool OverrideOnly = false,
        int? TokensLeftLt = null,
        int? TokensLeftGt = null,
        string? Sort = null);

    public sealed record AdminUserListItemDto(
        string UserId,
        string? Email,
        string? DisplayName,
        DateTime CreatedAtUtc,
        DateTime LastSeenUtc,
        string PlanKey,
        string SubscriptionStatus,
        int AiMonthlyTokenBudget,
        int AiTokensUsedThisPeriod,
        int TokensLeft,
        bool IsManuallyOverridden,
        bool IsAdminAccess,
        string AdminAccessSource,
        bool HasRoleAdminAssignment,
        bool CanGrantAdminRole,
        bool CanRevokeAdminRole,
        string? AdminRoleActionDisabledReason);

    public sealed record AdminUserListResponseDto(
        IReadOnlyList<AdminUserListItemDto> Items,
        int Page,
        int PageSize,
        int TotalCount);

    public sealed record AdminUserDetailDto(
        string UserId,
        string? Email,
        string? DisplayName,
        DateTime CreatedAtUtc,
        DateTime LastSeenUtc,
        string PlanKey,
        string SubscriptionStatus,
        int AiMonthlyTokenBudget,
        int AiTokensUsedThisPeriod,
        int TokensLeft,
        bool IsManuallyOverridden,
        bool IsAdminAccess,
        string AdminAccessSource,
        bool HasRoleAdminAssignment,
        bool CanGrantAdminRole,
        bool CanRevokeAdminRole,
        string? AdminRoleActionDisabledReason,
        string? OverridePlanKey,
        DateTime? OverrideAssignedUtc,
        string? OverrideAssignedBy,
        string? StripeCustomerId,
        string? StripeSubscriptionId);

    public sealed record AdminRoleChangeResponse(
        string UserId,
        string Action,
        AdminUserDetailDto User);

    public sealed record AdminCreateUserRequest(
        string? UserId,
        string? Email,
        string? DisplayName);

    public sealed record AdminUpdateUserRequest(
        string? Email,
        string? DisplayName);

    public sealed record AdminAuditEventDto(
        long Id,
        DateTime OccurredAtUtc,
        string AdminUserId,
        string? AdminEmail,
        string Action,
        string? TargetUserId,
        string? TargetEmail,
        string? DetailsJson);

    public sealed record AdminAuditQueryDto(
        int Page = 1,
        int PageSize = 50,
        string? AdminUserId = null,
        string? TargetUserId = null,
        string? Action = null,
        DateTime? FromUtc = null,
        DateTime? ToUtc = null);

    public sealed record AdminAuditListResponseDto(
        IReadOnlyList<AdminAuditEventDto> Items,
        int Page,
        int PageSize,
        int TotalCount);

    public sealed record AdminAdjustTokensRequest(
        int DeltaTokens,
        string? Reason);

    public sealed record AdminTokenOperationResponse(
        string UserId,
        int AiMonthlyTokenBudget,
        int AiTokensUsedThisPeriod,
        int TokensLeft,
        DateTimeOffset PeriodStartUtc);

    public sealed record AdminResetToFirstRunResponse(
        string UserId,
        int DeletedProjects,
        int DeletedOutlineTemplates,
        int DeletedExportTemplates,
        int DeletedExportPresets,
        int DeletedPromptPresets,
        int DeletedUsageEvents,
        int DeletedUsageAggregates,
        int DeletedUserEvents,
        int RemovedPlanOverrides,
        bool ResetToFreePlan,
        bool ExternalIdentityPreserved,
        AdminUserDetailDto User);

    public sealed record AdminDeleteCustomerResponse(
        string UserId,
        bool AlreadyDeleted,
        int DeletedProjects,
        int DeletedOutlineTemplates,
        int DeletedExportTemplates,
        int DeletedExportPresets,
        int DeletedPromptPresets,
        int DeletedAiActionHistoryEntries,
        int DeletedAiActionAppliedEvents,
        int DeletedUsageEvents,
        int DeletedUsageAggregates,
        int DeletedUserEvents,
        int DeletedTokenAdjustments,
        int RemovedPlanOverrides,
        bool DeletedUserProfile,
        bool DeletedEntitlement,
        bool ExternalIdentityPreserved,
        bool PreservedAuditTrail);
}
