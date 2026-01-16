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
        public WriterAiOpenAiOptions OpenAI { get; set; } = new();
    }

    public sealed class WriterAiStreamingOptions
    {
        public bool Enabled { get; set; } = false;
    }

    public sealed class WriterAiUiOptions
    {
        public bool ShowAiMenu { get; set; } = true;
    }

    public sealed class WriterAiOpenAiOptions
    {
        public bool Enabled { get; set; } = false;
<<<<<<< HEAD
=======
        public string? ApiKey { get; set; }
>>>>>>> ebb7526 (Implemented export of md and html)
        public string? BaseUrl { get; set; }
        public string TextModel { get; set; } = "gpt-4.1-mini";
        public string ImageModel { get; set; } = "gpt-image-1";
        public int TimeoutSeconds { get; set; } = 60;
        public int MaxOutputTokens { get; set; } = 800;
        public string? Organization { get; set; }
    }
}
