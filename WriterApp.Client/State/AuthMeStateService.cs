using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using WriterApp.Application.Security;
using WriterApp.Client.Utilities;
using WriterApp.Shared.Billing;

namespace WriterApp.Client.State
{
    public sealed class AuthMeStateService
    {
        private readonly HttpClient _http;
        private readonly DeletedAccountStateService _deletedAccountStateService;
        private readonly DuplicateAccountStateService _duplicateAccountStateService;
        private bool _refreshInProgress;

        public AuthMeStateService(HttpClient http, DeletedAccountStateService deletedAccountStateService, DuplicateAccountStateService duplicateAccountStateService)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _deletedAccountStateService = deletedAccountStateService ?? throw new ArgumentNullException(nameof(deletedAccountStateService));
            _duplicateAccountStateService = duplicateAccountStateService ?? throw new ArgumentNullException(nameof(duplicateAccountStateService));
        }

        public event Action? Changed;

        public bool IsLoaded { get; private set; }
        public bool IsAuthenticated { get; private set; }
        public bool IsDeletedAccount { get; private set; }
        public bool IsDuplicateAccount { get; private set; }
        public string DuplicateAccountMessage { get; private set; } = DuplicateAccountStateService.DefaultMessage;
        public string DeletedAccountMessage { get; private set; } = DeletedAccountStateService.DefaultMessage;
        public string PlanKey { get; private set; } = "Free";
        public string EffectivePlanKey { get; private set; } = "Free";
        public string SubscriptionStatus { get; private set; } = string.Empty;
        public string StripeCustomerId { get; private set; } = string.Empty;
        public DateTimeOffset? CurrentPeriodEndUtc { get; private set; }
        public bool CancelAtPeriodEnd { get; private set; }
        public bool IsPaidAccessActive { get; private set; }
        public bool IsAdminAccess { get; private set; }
        public string AdminAccessSource { get; private set; } = "None";
        public bool IsBootstrapAdminSession => string.Equals(AdminAccessSource, "Bootstrap", StringComparison.Ordinal);
        public int AiMonthlyTokenBudget { get; private set; }
        public int AiTokensUsedThisPeriod { get; private set; }
        public DateTimeOffset PeriodStartUtc { get; private set; }
        public DateTimeOffset EntitlementUpdatedUtc { get; private set; }

        public void Reset()
        {
            _refreshInProgress = false;
            _deletedAccountStateService.Clear();
            _duplicateAccountStateService.Clear();
            Apply(false, "Free", "Free", string.Empty, string.Empty, null, false, false, false, "None", 0, 0, DateTimeOffset.MinValue, DateTimeOffset.MinValue);
        }

        public async Task RefreshAsync(bool force = false, DateTimeOffset? serverEntitlementUpdatedUtc = null)
        {
            if (_refreshInProgress)
            {
                return;
            }

            if (!force && IsLoaded)
            {
                if (!serverEntitlementUpdatedUtc.HasValue || serverEntitlementUpdatedUtc.Value <= EntitlementUpdatedUtc)
                {
                    return;
                }
            }

            _refreshInProgress = true;
            try
            {
                string endpoint = force ? "/api/auth/me?force=1" : "/api/auth/me";
                using HttpResponseMessage response = await _http.GetAsync(endpoint);
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    DeletedAccountApiResponse? deleted = await DeletedAccountApiResponseReader.TryReadAsync(response);
                    if (deleted is not null)
                    {
                        _deletedAccountStateService.MarkDeleted(deleted.Message);
                        _duplicateAccountStateService.Clear();
                        ApplyDeleted(deleted.Message);
                        return;
                    }
                }

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    AuthDuplicateAccountDto? duplicate = await DuplicateAccountApiResponseReader.TryReadAsync(response);
                    if (duplicate is not null)
                    {
                        _deletedAccountStateService.Clear();
                        _duplicateAccountStateService.MarkDuplicate(duplicate);
                        ApplyDuplicate(duplicate.Message);
                        return;
                    }
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    _deletedAccountStateService.Clear();
                    _duplicateAccountStateService.Clear();
                    Apply(false, "Free", "Free", string.Empty, string.Empty, null, false, false, false, "None", 0, 0, DateTimeOffset.MinValue, DateTimeOffset.MinValue);
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _deletedAccountStateService.Clear();
                    _duplicateAccountStateService.Clear();
                    Apply(false, "Free", "Free", string.Empty, string.Empty, null, false, false, false, "None", 0, 0, DateTimeOffset.MinValue, DateTimeOffset.MinValue);
                    return;
                }

                AuthMeDto? auth = await response.Content.ReadFromJsonAsync<AuthMeDto>();
                if (auth is null)
                {
                    _deletedAccountStateService.Clear();
                    _duplicateAccountStateService.Clear();
                    Apply(false, "Free", "Free", string.Empty, string.Empty, null, false, false, false, "None", 0, 0, DateTimeOffset.MinValue, DateTimeOffset.MinValue);
                    return;
                }

                _deletedAccountStateService.Clear();
                _duplicateAccountStateService.Clear();
                Apply(
                    auth.IsAuthenticated,
                    NormalizePlanKey(auth.PlanKey),
                    NormalizePlanKey(auth.EffectivePlanKey ?? auth.PlanKey),
                    NormalizeSubscriptionStatus(auth.SubscriptionStatus),
                    auth.StripeCustomerId ?? string.Empty,
                    auth.CurrentPeriodEndUtc,
                    auth.CancelAtPeriodEnd,
                    auth.IsPaidAccessActive,
                    auth.IsAdminAccess,
                    NormalizeAdminAccessSource(auth.AdminAccessSource),
                    Math.Max(0, auth.AiMonthlyTokenBudget),
                    Math.Max(0, auth.AiTokensUsedThisPeriod),
                    auth.PeriodStartUtc,
                    auth.EntitlementUpdatedUtc);
            }
            catch
            {
            }
            finally
            {
                _refreshInProgress = false;
            }
        }

        private void Apply(bool isAuthenticated, string planKey, string effectivePlanKey, string subscriptionStatus, string stripeCustomerId, DateTimeOffset? currentPeriodEndUtc, bool cancelAtPeriodEnd, bool isPaidAccessActive, bool isAdminAccess, string adminAccessSource, int budget, int used, DateTimeOffset periodStartUtc, DateTimeOffset entitlementUpdatedUtc)
        {
            bool changed = IsLoaded != true
                || IsAuthenticated != isAuthenticated
                || IsDeletedAccount
                || IsDuplicateAccount
                || !string.Equals(PlanKey, planKey, StringComparison.Ordinal)
                || !string.Equals(EffectivePlanKey, effectivePlanKey, StringComparison.Ordinal)
                || !string.Equals(SubscriptionStatus, subscriptionStatus, StringComparison.Ordinal)
                || !string.Equals(StripeCustomerId, stripeCustomerId, StringComparison.Ordinal)
                || CurrentPeriodEndUtc != currentPeriodEndUtc
                || CancelAtPeriodEnd != cancelAtPeriodEnd
                || IsPaidAccessActive != isPaidAccessActive
                || IsAdminAccess != isAdminAccess
                || !string.Equals(AdminAccessSource, adminAccessSource, StringComparison.Ordinal)
                || AiMonthlyTokenBudget != budget
                || AiTokensUsedThisPeriod != used
                || PeriodStartUtc != periodStartUtc
                || EntitlementUpdatedUtc != entitlementUpdatedUtc;

            IsLoaded = true;
            IsAuthenticated = isAuthenticated;
            IsDeletedAccount = false;
            IsDuplicateAccount = false;
            DeletedAccountMessage = DeletedAccountStateService.DefaultMessage;
            DuplicateAccountMessage = DuplicateAccountStateService.DefaultMessage;
            PlanKey = planKey;
            EffectivePlanKey = effectivePlanKey;
            SubscriptionStatus = subscriptionStatus;
            StripeCustomerId = stripeCustomerId;
            CurrentPeriodEndUtc = currentPeriodEndUtc;
            CancelAtPeriodEnd = cancelAtPeriodEnd;
            IsPaidAccessActive = isPaidAccessActive;
            IsAdminAccess = isAdminAccess;
            AdminAccessSource = adminAccessSource;
            AiMonthlyTokenBudget = budget;
            AiTokensUsedThisPeriod = used;
            PeriodStartUtc = periodStartUtc;
            EntitlementUpdatedUtc = entitlementUpdatedUtc;

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        private void ApplyDeleted(string? message)
        {
            string normalizedMessage = string.IsNullOrWhiteSpace(message)
                ? DeletedAccountStateService.DefaultMessage
                : message.Trim();

            bool changed = IsLoaded != true
                || IsAuthenticated
                || !IsDeletedAccount
                || IsDuplicateAccount
                || !string.Equals(DeletedAccountMessage, normalizedMessage, StringComparison.Ordinal)
                || !string.Equals(PlanKey, "Free", StringComparison.Ordinal)
                || !string.Equals(EffectivePlanKey, "Free", StringComparison.Ordinal)
                || !string.Equals(SubscriptionStatus, string.Empty, StringComparison.Ordinal)
                || !string.Equals(StripeCustomerId, string.Empty, StringComparison.Ordinal)
                || CurrentPeriodEndUtc != null
                || CancelAtPeriodEnd
                || IsPaidAccessActive
                || IsAdminAccess
                || !string.Equals(AdminAccessSource, "None", StringComparison.Ordinal)
                || AiMonthlyTokenBudget != 0
                || AiTokensUsedThisPeriod != 0
                || PeriodStartUtc != DateTimeOffset.MinValue
                || EntitlementUpdatedUtc != DateTimeOffset.MinValue;

            IsLoaded = true;
            IsAuthenticated = false;
            IsDeletedAccount = true;
            IsDuplicateAccount = false;
            DeletedAccountMessage = normalizedMessage;
            DuplicateAccountMessage = DuplicateAccountStateService.DefaultMessage;
            PlanKey = "Free";
            EffectivePlanKey = "Free";
            SubscriptionStatus = string.Empty;
            StripeCustomerId = string.Empty;
            CurrentPeriodEndUtc = null;
            CancelAtPeriodEnd = false;
            IsPaidAccessActive = false;
            IsAdminAccess = false;
            AdminAccessSource = "None";
            AiMonthlyTokenBudget = 0;
            AiTokensUsedThisPeriod = 0;
            PeriodStartUtc = DateTimeOffset.MinValue;
            EntitlementUpdatedUtc = DateTimeOffset.MinValue;

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        private void ApplyDuplicate(string? message)
        {
            string normalizedMessage = string.IsNullOrWhiteSpace(message)
                ? DuplicateAccountStateService.DefaultMessage
                : message.Trim();

            bool changed = IsLoaded != true
                || IsAuthenticated
                || IsDeletedAccount
                || !IsDuplicateAccount
                || !string.Equals(DuplicateAccountMessage, normalizedMessage, StringComparison.Ordinal)
                || !string.Equals(PlanKey, "Free", StringComparison.Ordinal)
                || !string.Equals(EffectivePlanKey, "Free", StringComparison.Ordinal)
                || !string.Equals(SubscriptionStatus, string.Empty, StringComparison.Ordinal)
                || !string.Equals(StripeCustomerId, string.Empty, StringComparison.Ordinal)
                || CurrentPeriodEndUtc != null
                || CancelAtPeriodEnd
                || IsPaidAccessActive
                || IsAdminAccess
                || !string.Equals(AdminAccessSource, "None", StringComparison.Ordinal)
                || AiMonthlyTokenBudget != 0
                || AiTokensUsedThisPeriod != 0
                || PeriodStartUtc != DateTimeOffset.MinValue
                || EntitlementUpdatedUtc != DateTimeOffset.MinValue;

            IsLoaded = true;
            IsAuthenticated = false;
            IsDeletedAccount = false;
            IsDuplicateAccount = true;
            DeletedAccountMessage = DeletedAccountStateService.DefaultMessage;
            DuplicateAccountMessage = normalizedMessage;
            PlanKey = "Free";
            EffectivePlanKey = "Free";
            SubscriptionStatus = string.Empty;
            StripeCustomerId = string.Empty;
            CurrentPeriodEndUtc = null;
            CancelAtPeriodEnd = false;
            IsPaidAccessActive = false;
            IsAdminAccess = false;
            AdminAccessSource = "None";
            AiMonthlyTokenBudget = 0;
            AiTokensUsedThisPeriod = 0;
            PeriodStartUtc = DateTimeOffset.MinValue;
            EntitlementUpdatedUtc = DateTimeOffset.MinValue;

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        private static string NormalizePlanKey(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "Free";
            }

            string normalized = raw.Trim().ToLowerInvariant();
            return normalized switch
            {
                "professional" => "Professional",
                "pro" => "Professional",
                "standard" => "Standard",
                _ => "Free"
            };
        }

        private static string NormalizeSubscriptionStatus(string? raw)
        {
            return BillingSubscriptionPolicy.NormalizeStatus(raw);
        }

        private static string NormalizeAdminAccessSource(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "None";
            }

            string normalized = raw.Trim().ToLowerInvariant();
            return normalized switch
            {
                "role" => "Role",
                "bootstrap" => "Bootstrap",
                _ => "None"
            };
        }
    }
}
