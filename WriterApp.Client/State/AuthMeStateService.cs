using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using WriterApp.Application.Security;

namespace WriterApp.Client.State
{
    public sealed class AuthMeStateService
    {
        private readonly HttpClient _http;
        private bool _refreshInProgress;

        public AuthMeStateService(HttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public event Action? Changed;

        public bool IsLoaded { get; private set; }
        public bool IsAuthenticated { get; private set; }
        public string PlanKey { get; private set; } = "Free";
        public int AiMonthlyTokenBudget { get; private set; }
        public int AiTokensUsedThisPeriod { get; private set; }
        public DateTimeOffset PeriodStartUtc { get; private set; }

        public async Task RefreshAsync(bool force = false)
        {
            if (_refreshInProgress)
            {
                return;
            }

            if (!force && IsLoaded)
            {
                return;
            }

            _refreshInProgress = true;
            try
            {
                using HttpResponseMessage response = await _http.GetAsync("/api/auth/me");
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    Apply(false, "Free", 0, 0, DateTimeOffset.MinValue);
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Apply(false, "Free", 0, 0, DateTimeOffset.MinValue);
                    return;
                }

                AuthMeDto? auth = await response.Content.ReadFromJsonAsync<AuthMeDto>();
                if (auth is null)
                {
                    Apply(false, "Free", 0, 0, DateTimeOffset.MinValue);
                    return;
                }

                Apply(
                    auth.IsAuthenticated,
                    NormalizePlanKey(auth.PlanKey),
                    Math.Max(0, auth.AiMonthlyTokenBudget),
                    Math.Max(0, auth.AiTokensUsedThisPeriod),
                    auth.PeriodStartUtc);
            }
            catch
            {
            }
            finally
            {
                _refreshInProgress = false;
            }
        }

        private void Apply(bool isAuthenticated, string planKey, int budget, int used, DateTimeOffset periodStartUtc)
        {
            bool changed = IsLoaded != true
                || IsAuthenticated != isAuthenticated
                || !string.Equals(PlanKey, planKey, StringComparison.Ordinal)
                || AiMonthlyTokenBudget != budget
                || AiTokensUsedThisPeriod != used
                || PeriodStartUtc != periodStartUtc;

            IsLoaded = true;
            IsAuthenticated = isAuthenticated;
            PlanKey = planKey;
            AiMonthlyTokenBudget = budget;
            AiTokensUsedThisPeriod = used;
            PeriodStartUtc = periodStartUtc;

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
