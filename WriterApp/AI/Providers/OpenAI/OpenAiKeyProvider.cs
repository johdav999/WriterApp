using System;

namespace WriterApp.AI.Providers.OpenAI
{
    /// <summary>
    /// Centralized OpenAI API key resolver. Never source this from appsettings.
    /// </summary>
    public sealed class OpenAiKeyProvider
    {
        private OpenAiKeyProvider(string? apiKey)
        {
            ApiKey = string.IsNullOrWhiteSpace(apiKey) ? string.Empty : apiKey.Trim();
        }

        public string ApiKey { get; }

        public bool HasKey => !string.IsNullOrWhiteSpace(ApiKey);

        public static OpenAiKeyProvider FromEnvironment()
        {
            return new OpenAiKeyProvider(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        }
    }
}
