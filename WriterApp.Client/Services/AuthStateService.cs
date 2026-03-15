using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Security;
using WriterApp.Client.State;
using WriterApp.Client.Utilities;

namespace WriterApp.Client.Services
{
    public sealed class AuthStateService
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

        private readonly HttpClient _http;
        private readonly ILogger<AuthStateService> _logger;
        private readonly DeletedAccountStateService _deletedAccountStateService;
        private readonly DuplicateAccountStateService _duplicateAccountStateService;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        private AuthState _cached = AuthState.Anonymous;
        private bool _hasResolvedState;
        private DateTimeOffset _cachedAtUtc = DateTimeOffset.MinValue;

        public AuthStateService(
            HttpClient http,
            ILogger<AuthStateService> logger,
            DeletedAccountStateService deletedAccountStateService,
            DuplicateAccountStateService duplicateAccountStateService)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _deletedAccountStateService = deletedAccountStateService ?? throw new ArgumentNullException(nameof(deletedAccountStateService));
            _duplicateAccountStateService = duplicateAccountStateService ?? throw new ArgumentNullException(nameof(duplicateAccountStateService));
        }

        public bool IsAuthenticated => _cached.IsAuthenticated;
        public bool HasResolvedState => _hasResolvedState;
        public string? UserId => _cached.UserId;
        public IReadOnlyDictionary<string, string> Claims => _cached.Claims;

        public async Task<AuthState> GetAsync(bool forceRefresh = false, CancellationToken ct = default)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (!forceRefresh && now - _cachedAtUtc < CacheDuration)
            {
                return _cached;
            }

            await _refreshLock.WaitAsync(ct);
            try
            {
                now = DateTimeOffset.UtcNow;
                if (!forceRefresh && now - _cachedAtUtc < CacheDuration)
                {
                    return _cached;
                }

                FetchResult result = await FetchAsync(ct);
                if (result.HasState && result.State is not null)
                {
                    _cached = result.State;
                    _hasResolvedState = true;
                    _logger.LogInformation(
                        "AuthState refresh from {Endpoint}. IsAuthenticated={IsAuthenticated} Reason={Reason}",
                        result.Endpoint,
                        _cached.IsAuthenticated,
                        result.Reason);
                }
                else
                {
                    _logger.LogWarning(
                        "AuthState refresh failed from {Endpoint}. Keeping previous state. Reason={Reason} IsAuthenticated={IsAuthenticated} HasResolvedState={HasResolvedState}",
                        result.Endpoint,
                        result.Reason,
                        _cached.IsAuthenticated,
                        _hasResolvedState);
                }

                _cachedAtUtc = DateTimeOffset.UtcNow;
                return _cached;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private async Task<FetchResult> FetchAsync(CancellationToken ct)
        {
            const string endpoint = "/api/auth/me";

            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, endpoint);
                using HttpResponseMessage response = await _http.SendAsync(request, ct);

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    DeletedAccountApiResponse? deleted = await DeletedAccountApiResponseReader.TryReadAsync(response, ct);
                    if (deleted is not null)
                    {
                        _deletedAccountStateService.MarkDeleted(deleted.Message);
                        _duplicateAccountStateService.Clear();
                        return FetchResult.FromState(
                            endpoint,
                            AuthState.DeletedAccount(deleted.Message, endpoint),
                            DeletedAccountApiResponseReader.DeletedCode);
                    }
                }

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    AuthDuplicateAccountDto? duplicate = await DuplicateAccountApiResponseReader.TryReadAsync(response, ct);
                    if (duplicate is not null)
                    {
                        _deletedAccountStateService.Clear();
                        _duplicateAccountStateService.MarkDuplicate(duplicate);
                        return FetchResult.FromState(
                            endpoint,
                            AuthState.DuplicateAccount(duplicate.Message, endpoint),
                            AuthDuplicateAccountDto.DuplicateCode);
                    }
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    _duplicateAccountStateService.Clear();
                    return FetchResult.FromState(
                        endpoint,
                        AuthState.Anonymous,
                        $"{(int)response.StatusCode} {response.StatusCode}");
                }

                if (!response.IsSuccessStatusCode)
                {
                    return FetchResult.NoState(
                        endpoint,
                        $"Unexpected status {(int)response.StatusCode}");
                }

                AuthMeDto? auth = await response.Content.ReadFromJsonAsync<AuthMeDto>(cancellationToken: ct);
                if (auth is null)
                {
                    return FetchResult.NoState(
                        endpoint,
                        "Response body could not be parsed");
                }

                if (!auth.IsAuthenticated)
                {
                    _deletedAccountStateService.Clear();
                    _duplicateAccountStateService.Clear();
                    return FetchResult.FromState(
                        endpoint,
                        AuthState.Anonymous,
                        "200 anonymous");
                }

                Dictionary<string, string> claims = new(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(auth.Name))
                {
                    claims["name"] = auth.Name;
                }

                if (!string.IsNullOrWhiteSpace(auth.Email))
                {
                    claims["email"] = auth.Email;
                }

                if (auth.Roles.Count > 0)
                {
                    claims["roles"] = string.Join(',', auth.Roles);
                }

                AuthState state = new(
                    IsAuthenticated: true,
                    Provider: endpoint,
                    UserId: auth.UserId,
                    Claims: claims);
                _deletedAccountStateService.Clear();
                _duplicateAccountStateService.Clear();
                return FetchResult.FromState(
                    endpoint,
                    state,
                    "200 OK");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AuthState probe request failed for {Endpoint}.", endpoint);
                return FetchResult.NoState(
                    endpoint,
                    "Network or parse exception");
            }
        }

        private sealed record FetchResult(
            bool HasState,
            AuthState? State,
            string Endpoint,
            string Reason)
        {
            public static FetchResult FromState(string endpoint, AuthState state, string reason) =>
                new(
                    HasState: true,
                    State: state,
                    Endpoint: endpoint,
                    Reason: reason);

            public static FetchResult NoState(string endpoint, string reason) =>
                new(
                    HasState: false,
                    State: null,
                    Endpoint: endpoint,
                    Reason: reason);
        }
    }
}
