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
        public int ContractVersion { get; init; }
        public System.Guid? ProjectId { get; init; }
        public System.Guid? DocumentId { get; init; }
        public long? ExpectedMetadataRevision { get; init; }
        public string? ExpectedDocumentVersion { get; init; }
    }

    public sealed record CoverGenerationResponse(IReadOnlyList<string> ImageUrls, int ContractVersion = 0,
        System.Guid? ProjectId = null, System.Guid? DocumentId = null, long? MetadataRevision = null, string? DocumentVersion = null,
        IReadOnlyList<CoverAssetIdentity?>? Assets = null);
}
