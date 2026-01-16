using System;

namespace WriterApp.AI.Abstractions
{
    public sealed class AiProviderException : InvalidOperationException
    {
        public AiProviderException(string providerId, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            ProviderId = providerId;
        }

        public string ProviderId { get; }
    }
}
