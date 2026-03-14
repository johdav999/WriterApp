using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using WriterApp.Application.Security;
using WriterApp.Client.Utilities;

namespace WriterApp.Client.State
{
    public sealed class AuthMeStateService
    {
        private readonly HttpClient _http;
        private readonly DeletedAccountStateService _deletedAccountStateService;
        private bool _refreshInProgress;

        public AuthMeStateService(HttpClient http, DeletedAccountStateService deletedAccountStateService)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _deletedAccountStateService = deletedAccountStateService ?? throw new ArgumentNullException(nameof(deletedAccountStateService));
        }

        public event Action? Changed;

        public bool IsLoaded { get; private set; }
        public bool IsAuthenticated { get; private set; }
        public bool IsDeletedAccount { get; private set; }
        public string DeletedAccountMessage { get; private set; } = DeletedAccountStateService.DefaultMessage;
        public string PlanKey { get; private set; } = "Free";
        public int AiMonthlyTokenBudget { get; private set; }
        public int AiTokensUsedThisPeriod { get; private set; }
        public DateTimeOffset PeriodStartUtc { get; private set; }
        public DateTimeOffset EntitlementUpdatedUtc { get; private set; }

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
                        ApplyDeleted(deleted.Message);
                        return;
                    }
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    _deletedAccountStateService.Clear();
                    Apply(false, "Free", 0, 0, DateTimeOffset.MinValue, DateTimeOffset.MinValue);
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _deletedAccountStateService.Clear();
                    Apply(false, "Free", 0, 0, DateTimeOffset.MinValue, DateTimeOffset.MinValue);
                    return;
                }

                AuthMeDto? auth = await response.Content.ReadFromJsonAsync<AuthMeDto>();
                if (auth is null)
                {
                    _deletedAccountStateService.Clear();
                    Apply(false, "Free", 0, 0, DateTimeOffset.MinValue, DateTimeOffset.MinValue);
                    return;
                }

                _deletedAccountStateService.Clear();
                Apply(
                    auth.IsAuthenticated,
                    NormalizePlanKey(auth.PlanKey),
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

        private void Apply(bool isAuthenticated, string planKey, int budget, int used, DateTimeOffset periodStartUtc, DateTimeOffset entitlementUpdatedUtc)
        {
            bool changed = IsLoaded != true
                || IsAuthenticated != isAuthenticated
                || IsDeletedAccount
                || !string.Equals(PlanKey, planKey, StringComparison.Ordinal)
                || AiMonthlyTokenBudget != budget
                || AiTokensUsedThisPeriod != used
                || PeriodStartUtc != periodStartUtc
                || EntitlementUpdatedUtc != entitlementUpdatedUtc;

            IsLoaded = true;
            IsAuthenticated = isAuthenticated;
            IsDeletedAccount = false;
            DeletedAccountMessage = DeletedAccountStateService.DefaultMessage;
            PlanKey = planKey;
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
                || !string.Equals(DeletedAccountMessage, normalizedMessage, StringComparison.Ordinal)
                || !string.Equals(PlanKey, "Free", StringComparison.Ordinal)
                || AiMonthlyTokenBudget != 0
                || AiTokensUsedThisPeriod != 0
                || PeriodStartUtc != DateTimeOffset.MinValue
                || EntitlementUpdatedUtc != DateTimeOffset.MinValue;

            IsLoaded = true;
            IsAuthenticated = false;
            IsDeletedAccount = true;
            DeletedAccountMessage = normalizedMessage;
            PlanKey = "Free";
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
    }
}
