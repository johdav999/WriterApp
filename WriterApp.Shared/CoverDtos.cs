using System.Collections.Generic;

namespace WriterApp.Shared
{
    public sealed class CoverPrompt
    {
        public string? Description { get; init; }

        public string? Genre { get; init; }

        public string? Mood { get; init; }

        public string? Style { get; init; }

        public string? ColorPalette { get; init; }
    }

    public sealed record CoverGenerationResponse(IReadOnlyList<string> ImageUrls);
}
