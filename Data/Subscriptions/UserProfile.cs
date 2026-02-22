using System;

namespace WriterApp.Data.Subscriptions
{
    public sealed class UserProfile
    {
        public string UserId { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public DateTime CreatedUtc { get; set; }
        public bool HasOnboarded { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }
}
