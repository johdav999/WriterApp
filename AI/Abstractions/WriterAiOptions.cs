namespace WriterApp.AI.Abstractions
{
    public sealed class WriterAiOptions
    {
        public bool Enabled { get; set; } = false;
        public WriterAiProviderOptions Providers { get; set; } = new();
        public WriterAiStreamingOptions Streaming { get; set; } = new();
        public WriterAiUiOptions UI { get; set; } = new();
    }

    public sealed class WriterAiProviderOptions
    {
        public string DefaultTextProviderId { get; set; } = "mock-text";
        public string DefaultImageProviderId { get; set; } = "mock-image";
        public bool AllowProviderFallback { get; set; } = true;
    }

    public sealed class WriterAiStreamingOptions
    {
        public bool Enabled { get; set; } = false;
    }

    public sealed class WriterAiUiOptions
    {
        public bool ShowAiMenu { get; set; } = true;
    }
}
