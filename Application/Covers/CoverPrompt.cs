using System;

namespace WriterApp.Application.Covers
{
    public sealed class CoverImageGenerationException : Exception
    {
        public CoverImageGenerationException(string code, string message)
            : base(message)
        {
            Code = string.IsNullOrWhiteSpace(code) ? "ai.provider_unavailable" : code.Trim();
        }

        public string Code { get; }
    }
}
