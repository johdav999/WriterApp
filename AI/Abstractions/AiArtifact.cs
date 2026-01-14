using System;
using System.Collections.Generic;

namespace WriterApp.AI.Abstractions
{
    public sealed record AiArtifact(
        Guid ArtifactId,
        AiModality Modality,
        string MimeType,
        string? TextContent,
        byte[]? BinaryContent,
        Dictionary<string, object>? Metadata);
}
