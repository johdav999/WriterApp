namespace WriterApp.Application.Security
{
    public sealed class WriterAuthOptions
    {
        public bool DevAutoLogin { get; set; } = false;
        public string DevAutoLoginEmail { get; set; } = "dev@local";
        public string DevAutoLoginPassword { get; set; } = "DevPassword123!";
        public string? DevUserIdFallback { get; set; }
        public string? AdminEmail { get; set; }
    }
}
