using System.Net;
using System.Net.Http.Json;

namespace WriterApp.Client.Services
{
    public sealed class OnboardingService
    {
        private readonly HttpClient _http;

        public OnboardingService(HttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public async Task<OnboardingState> GetStateAsync()
        {
            using HttpResponseMessage response = await _http.GetAsync("/api/onboarding/state");
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return OnboardingState.Default;
            }

            response.EnsureSuccessStatusCode();
            OnboardingStateResponse? payload = await response.Content.ReadFromJsonAsync<OnboardingStateResponse>();
            if (payload is null)
            {
                return OnboardingState.Default;
            }

            return payload.ToState();
        }

        public async Task SetIntentAsync(string intent)
        {
            SetOnboardingIntentRequest request = new(intent);
            using HttpResponseMessage response = await _http.PostAsJsonAsync("/api/onboarding/intent", request);
            response.EnsureSuccessStatusCode();
        }

        public async Task SetStepAsync(int step)
        {
            SetOnboardingStepRequest request = new(step);
            using HttpResponseMessage response = await _http.PostAsJsonAsync("/api/onboarding/step", request);
            response.EnsureSuccessStatusCode();
        }

        public async Task CompleteAsync()
        {
            using HttpResponseMessage response = await _http.PostAsync("/api/onboarding/complete", content: null);
            response.EnsureSuccessStatusCode();
        }

        public async Task TrackEventAsync(string eventName, object? metadata = null)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                return;
            }

            TrackOnboardingEventRequest request = new(eventName.Trim(), metadata);
            using HttpResponseMessage response = await _http.PostAsJsonAsync("/api/onboarding/event", request);
            response.EnsureSuccessStatusCode();
        }

        private sealed record OnboardingStateResponse(
            bool HasCompletedOnboarding,
            int OnboardingStep,
            string? PrimaryWritingIntent,
            DateTimeOffset? OnboardingStartedUtc,
            DateTimeOffset? OnboardingCompletedUtc)
        {
            public OnboardingState ToState()
            {
                return new OnboardingState(
                    HasCompletedOnboarding,
                    OnboardingStep,
                    PrimaryWritingIntent,
                    OnboardingStartedUtc,
                    OnboardingCompletedUtc);
            }
        }

        private sealed record SetOnboardingIntentRequest(string PrimaryWritingIntent);
        private sealed record SetOnboardingStepRequest(int Step);
        private sealed record TrackOnboardingEventRequest(string EventName, object? Metadata);
    }

    public sealed record OnboardingState(
        bool HasCompletedOnboarding,
        int OnboardingStep,
        string? PrimaryWritingIntent,
        DateTimeOffset? OnboardingStartedUtc,
        DateTimeOffset? OnboardingCompletedUtc)
    {
        public static OnboardingState Default { get; } =
            new(
                HasCompletedOnboarding: false,
                OnboardingStep: 0,
                PrimaryWritingIntent: null,
                OnboardingStartedUtc: null,
                OnboardingCompletedUtc: null);
    }
}
