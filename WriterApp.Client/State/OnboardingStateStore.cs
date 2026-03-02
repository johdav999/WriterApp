using WriterApp.Client.Services;

namespace WriterApp.Client.State
{
    public sealed class OnboardingStateStore
    {
        private readonly OnboardingService _onboardingService;

        public OnboardingStateStore(OnboardingService onboardingService)
        {
            _onboardingService = onboardingService ?? throw new ArgumentNullException(nameof(onboardingService));
        }

        public event Action? Changed;

        public OnboardingState Current { get; private set; } = OnboardingState.Default;

        public async Task RefreshAsync()
        {
            OnboardingState next = await _onboardingService.GetStateAsync();
            if (Current == next)
            {
                return;
            }

            Current = next;
            Changed?.Invoke();
        }
    }
}
