using System;
using System.Collections.Generic;

namespace WriterApp.Domain.Documents
{
    /// <summary>
    /// A document chapter that can be reordered independently of content.
    /// </summary>
    public record Chapter
    {
        public Guid ChapterId { get; init; } = Guid.NewGuid();

        public int Order { get; init; }

        public string Title { get; init; } = string.Empty;

        public string? Summary { get; init; }

        public List<Section> Sections { get; init; } = new();

        public Chapter()
        {
        }
    }
}
