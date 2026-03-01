using System;

namespace WriterApp.Domain.Documents
{
    public sealed record DocumentArtifact(Guid ArtifactId, string MimeType, string? Base64Data, string? DataUrl);
}
