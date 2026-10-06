namespace WriterApp.Data.Documents;

// Immutable validated bytes/provenance. A cache insertion never updates project metadata.
public sealed class CoverAssetRecord
{
    public Guid Id { get; set; }
    public int Version { get; set; } = 1;
    public string OwnerUserId { get; set; } = "";
    public Guid ProjectId { get; set; }
    public Guid SourceDocumentId { get; set; }
    public long SourceMetadataRevision { get; set; }
    public string SourceDocumentVersion { get; set; } = "";
    public string ReferenceHash { get; set; } = "";
    public string RemoteReference { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public string MediaType { get; set; } = "image/png";
    public byte[] Bytes { get; set; } = [];
}
