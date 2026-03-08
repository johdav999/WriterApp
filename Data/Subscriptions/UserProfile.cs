using System;

namespace WriterApp.Data.Subscriptions
{
    public sealed class UserProfile
    {
        public string UserId { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public DateTime CreatedUtc { get; set; }
        public bool HasOnboarded { get; set; }
        public bool HasCompletedOnboarding { get; set; }
        public int OnboardingStep { get; set; }
        public DateTimeOffset? OnboardingStartedUtc { get; set; }
        public DateTimeOffset? OnboardingCompletedUtc { get; set; }
        public string? PrimaryWritingIntent { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }
}
