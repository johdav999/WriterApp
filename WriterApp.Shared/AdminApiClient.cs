using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WriterApp.Shared
{
    public sealed class AdminApiClient
    {
        private readonly HttpClient _http;

        public AdminApiClient(HttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public async Task<AdminUserListResponseDto> GetUsersAsync(AdminUserQueryDto query, CancellationToken ct = default)
        {
            string url = BuildUsersUrl(query);
            AdminUserListResponseDto? payload = await _http.GetFromJsonAsync<AdminUserListResponseDto>(url, ct);
            return payload ?? new AdminUserListResponseDto(Array.Empty<AdminUserListItemDto>(), query.Page, query.PageSize, 0);
        }

        public async Task<AdminUserDetailDto?> GetUserAsync(string userId, CancellationToken ct = default)
        {
            return await _http.GetFromJsonAsync<AdminUserDetailDto>($"api/admin/users/{Uri.EscapeDataString(userId)}", ct);
        }

        public async Task<AdminUserDetailDto> CreateUserAsync(AdminCreateUserRequest request, CancellationToken ct = default)
        {
            using HttpResponseMessage response = await _http.PostAsJsonAsync("api/admin/users", request, ct);
            await EnsureSuccess(response, ct);
            AdminUserDetailDto? payload = await response.Content.ReadFromJsonAsync<AdminUserDetailDto>(cancellationToken: ct);
            return payload ?? throw new InvalidOperationException("Admin API returned an empty create-user response.");
        }

        public async Task<AdminUserDetailDto> UpdateUserAsync(string userId, AdminUpdateUserRequest request, CancellationToken ct = default)
        {
            using HttpResponseMessage response = await _http.PutAsJsonAsync($"api/admin/users/{Uri.EscapeDataString(userId)}", request, ct);
            await EnsureSuccess(response, ct);
            AdminUserDetailDto? payload = await response.Content.ReadFromJsonAsync<AdminUserDetailDto>(cancellationToken: ct);
            return payload ?? throw new InvalidOperationException("Admin API returned an empty update-user response.");
        }

        public async Task<AdminDeleteCustomerResponse> DeleteUserAsync(string userId, CancellationToken ct = default)
        {
            using HttpResponseMessage response = await _http.DeleteAsync($"api/admin/users/{Uri.EscapeDataString(userId)}", ct);
            await EnsureSuccess(response, ct);
            AdminDeleteCustomerResponse? payload = await response.Content.ReadFromJsonAsync<AdminDeleteCustomerResponse>(cancellationToken: ct);
            return payload ?? throw new InvalidOperationException("Admin API returned an empty delete-user response.");
        }

        public async Task<AdminRoleChangeResponse> GrantAdminAsync(string userId, CancellationToken ct = default)
        {
            using HttpResponseMessage response =
                await _http.PostAsync($"api/admin/users/{Uri.EscapeDataString(userId)}/grant-admin", content: null, ct);
            await EnsureSuccess(response, ct);
            AdminRoleChangeResponse? payload = await response.Content.ReadFromJsonAsync<AdminRoleChangeResponse>(cancellationToken: ct);
            return payload ?? throw new InvalidOperationException("Admin API returned an empty grant-admin response.");
        }

        public async Task<AdminRoleChangeResponse> RevokeAdminAsync(string userId, CancellationToken ct = default)
        {
            using HttpResponseMessage response =
                await _http.PostAsync($"api/admin/users/{Uri.EscapeDataString(userId)}/revoke-admin", content: null, ct);
            await EnsureSuccess(response, ct);
            AdminRoleChangeResponse? payload = await response.Content.ReadFromJsonAsync<AdminRoleChangeResponse>(cancellationToken: ct);
            return payload ?? throw new InvalidOperationException("Admin API returned an empty revoke-admin response.");
        }

        public async Task<AdminPlanOverrideResponse> SetPlanOverrideAsync(string userId, AdminSetPlanOverrideRequest request, CancellationToken ct = default)
        {
            using HttpResponseMessage response =
                await _http.PostAsJsonAsync($"api/admin/users/{Uri.EscapeDataString(userId)}/plan-override", request, ct);
            await EnsureSuccess(response, ct);
            AdminPlanOverrideResponse? payload = await response.Content.ReadFromJsonAsync<AdminPlanOverrideResponse>(cancellationToken: ct);
            return payload ?? throw new InvalidOperationException("Admin API returned an empty plan-override response.");
        }

        public async Task<AdminUserDetailDto> SyncStripeAsync(string userId, CancellationToken ct = default)
        {
            using HttpResponseMessage response =
                await _http.PostAsync($"api/admin/users/{Uri.EscapeDataString(userId)}/stripe/sync", content: null, ct);
            await EnsureSuccess(response, ct);
            AdminUserDetailDto? payload = await response.Content.ReadFromJsonAsync<AdminUserDetailDto>(cancellationToken: ct);
            return payload ?? throw new InvalidOperationException("Admin API returned an empty stripe-sync response.");
        }

        public async Task<AdminTokenOperationResponse> ResetTokensPeriodAsync(string userId, CancellationToken ct = default)
        {
            using HttpResponseMessage response =
                await _http.PostAsync($"api/admin/users/{Uri.EscapeDataString(userId)}/tokens/reset-period", content: null, ct);
            await EnsureSuccess(response, ct);
            AdminTokenOperationResponse? payload = await response.Content.ReadFromJsonAsync<AdminTokenOperationResponse>(cancellationToken: ct);
            return payload ?? throw new InvalidOperationException("Admin API returned an empty reset-tokens response.");
        }

        public async Task<AdminTokenOperationResponse> AdjustTokensAsync(string userId, AdminAdjustTokensRequest request, CancellationToken ct = default)
        {
            using HttpResponseMessage response =
                await _http.PostAsJsonAsync($"api/admin/users/{Uri.EscapeDataString(userId)}/tokens/adjust", request, ct);
            await EnsureSuccess(response, ct);
            AdminTokenOperationResponse? payload = await response.Content.ReadFromJsonAsync<AdminTokenOperationResponse>(cancellationToken: ct);
            return payload ?? throw new InvalidOperationException("Admin API returned an empty adjust-tokens response.");
        }

        public async Task<AdminResetToFirstRunResponse> ResetToFirstRunAsync(string userId, CancellationToken ct = default)
        {
            using HttpResponseMessage response =
                await _http.PostAsync($"api/admin/users/{Uri.EscapeDataString(userId)}/reset-first-run", content: null, ct);
            await EnsureSuccess(response, ct);
            AdminResetToFirstRunResponse? payload = await response.Content.ReadFromJsonAsync<AdminResetToFirstRunResponse>(cancellationToken: ct);
            return payload ?? throw new InvalidOperationException("Admin API returned an empty reset-first-run response.");
        }

        public async Task<AdminAuditListResponseDto> GetAuditAsync(AdminAuditQueryDto query, CancellationToken ct = default)
        {
            string url = BuildAuditUrl(query);
            AdminAuditListResponseDto? payload = await _http.GetFromJsonAsync<AdminAuditListResponseDto>(url, ct);
            return payload ?? new AdminAuditListResponseDto(Array.Empty<AdminAuditEventDto>(), query.Page, query.PageSize, 0);
        }

        public Task<string> ExportUsersCsvAsync(AdminUserQueryDto query, CancellationToken ct = default)
        {
            string url = BuildUsersCsvUrl(query);
            return _http.GetStringAsync(url, ct);
        }

        private static string BuildUsersUrl(AdminUserQueryDto query)
        {
            Dictionary<string, string?> parameters = new(StringComparer.Ordinal)
            {
                ["page"] = query.Page.ToString(CultureInfo.InvariantCulture),
                ["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture),
                ["q"] = query.Q,
                ["planKey"] = query.PlanKey,
                ["subscriptionStatus"] = query.SubscriptionStatus,
                ["overrideOnly"] = query.OverrideOnly ? "true" : null,
                ["tokensLeftLt"] = query.TokensLeftLt?.ToString(CultureInfo.InvariantCulture),
                ["tokensLeftGt"] = query.TokensLeftGt?.ToString(CultureInfo.InvariantCulture),
                ["sort"] = query.Sort
            };

            return BuildUrl("api/admin/users", parameters);
        }

        private static string BuildUsersCsvUrl(AdminUserQueryDto query)
        {
            Dictionary<string, string?> parameters = new(StringComparer.Ordinal)
            {
                ["page"] = query.Page.ToString(CultureInfo.InvariantCulture),
                ["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture),
                ["q"] = query.Q,
                ["planKey"] = query.PlanKey,
                ["subscriptionStatus"] = query.SubscriptionStatus,
                ["overrideOnly"] = query.OverrideOnly ? "true" : null,
                ["tokensLeftLt"] = query.TokensLeftLt?.ToString(CultureInfo.InvariantCulture),
                ["tokensLeftGt"] = query.TokensLeftGt?.ToString(CultureInfo.InvariantCulture),
                ["sort"] = query.Sort
            };

            return BuildUrl("api/admin/users/export.csv", parameters);
        }

        private static string BuildAuditUrl(AdminAuditQueryDto query)
        {
            Dictionary<string, string?> parameters = new(StringComparer.Ordinal)
            {
                ["page"] = query.Page.ToString(CultureInfo.InvariantCulture),
                ["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture),
                ["adminUserId"] = query.AdminUserId,
                ["targetUserId"] = query.TargetUserId,
                ["action"] = query.Action,
                ["fromUtc"] = query.FromUtc?.ToString("O", CultureInfo.InvariantCulture),
                ["toUtc"] = query.ToUtc?.ToString("O", CultureInfo.InvariantCulture)
            };

            return BuildUrl("api/admin/audit", parameters);
        }

        private static string BuildUrl(string path, IReadOnlyDictionary<string, string?> parameters)
        {
            StringBuilder builder = new(path);
            bool hasAny = false;
            foreach (KeyValuePair<string, string?> kvp in parameters)
            {
                if (string.IsNullOrWhiteSpace(kvp.Value))
                {
                    continue;
                }

                builder.Append(hasAny ? '&' : '?');
                builder.Append(Uri.EscapeDataString(kvp.Key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(kvp.Value));
                hasAny = true;
            }

            return builder.ToString();
        }

        private static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken ct)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            string content = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new HttpRequestException($"Admin API failed with status {(int)response.StatusCode} ({response.StatusCode}).");
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("message", out JsonElement messageElement))
                {
                    string? message = messageElement.GetString();
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        throw new HttpRequestException(message);
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore parse issues and throw generic error below.
            }

            throw new HttpRequestException(new StringBuilder()
                .Append("Admin API failed with status ")
                .Append((int)response.StatusCode)
                .Append(" (")
                .Append(response.StatusCode)
                .Append(')')
                .ToString());
        }
    }
}
