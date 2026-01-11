using System;
using System.Collections.Generic;

namespace WriterApp.Domain.Documents
{
    /// <summary>
    /// User-visible metadata for a document, including ownership and tagging.
    /// </summary>
    public record DocumentMetadata
    {
        public string Title { get; init; } = string.Empty;

        public string? Subtitle { get; init; }

        public string Author { get; init; } = string.Empty;

        public string Language { get; init; } = string.Empty;

        public string? Description { get; init; }

        public List<string> Tags { get; init; } = new();

        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

        public DateTime ModifiedUtc { get; init; } = DateTime.UtcNow;

        public DocumentMetadata()
        {
        }
    }
}
