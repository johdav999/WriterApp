namespace WriterApp.Client.State
{
    public sealed class OnboardingOverlayStateService
    {
        public event Action? Changed;

        public bool IsVisible { get; private set; }
        public int StepIndex { get; private set; }
        public int TotalSteps { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public string? TargetSelector { get; private set; }
        public string? StatusMessage { get; private set; }
        public bool IsBusy { get; private set; }
        public bool ShowActionButton { get; private set; }
        public string ActionButtonText { get; private set; } = "Action";
        public string NextButtonText { get; private set; } = "Next";

        public Func<Task>? OnNextAsync { get; private set; }
        public Func<Task>? OnSkipAsync { get; private set; }
        public Func<Task>? OnActionAsync { get; private set; }

        public void Set(
            bool isVisible,
            int stepIndex,
            int totalSteps,
            string title,
            string description,
            string? targetSelector,
            string? statusMessage,
            bool isBusy,
            bool showActionButton,
            string actionButtonText,
            string nextButtonText,
            Func<Task>? onNextAsync,
            Func<Task>? onSkipAsync,
            Func<Task>? onActionAsync)
        {
            IsVisible = isVisible;
            StepIndex = stepIndex;
            TotalSteps = totalSteps;
            Title = title;
            Description = description;
            TargetSelector = targetSelector;
            StatusMessage = statusMessage;
            IsBusy = isBusy;
            ShowActionButton = showActionButton;
            ActionButtonText = actionButtonText;
            NextButtonText = nextButtonText;
            OnNextAsync = onNextAsync;
            OnSkipAsync = onSkipAsync;
            OnActionAsync = onActionAsync;
            Changed?.Invoke();
        }

        public void Clear()
        {
            IsVisible = false;
            StepIndex = 0;
            TotalSteps = 0;
            Title = string.Empty;
            Description = string.Empty;
            TargetSelector = null;
            StatusMessage = null;
            IsBusy = false;
            ShowActionButton = false;
            ActionButtonText = "Action";
            NextButtonText = "Next";
            OnNextAsync = null;
            OnSkipAsync = null;
            OnActionAsync = null;
            Changed?.Invoke();
        }
    }
}
