using System.Text.Json;
using System.Text.Json.Serialization;
using WriterApp.Shared;

namespace WriterApp.Device.Shared.Storage;

public sealed record LocalCoverDraft(int Version, Guid Id, string Scope, Guid DocumentId, Guid ProjectId,
    Guid CloudDocumentId, Guid CloudProjectId, long SourceRevision, long MetadataRevision, string DocumentVersion,
    long CloudMetadataRevision, DateTimeOffset CreatedAt, IReadOnlyList<string> Images, int Selected,
    IReadOnlyList<CoverAssetIdentity?>? Assets = null, CoverEditResponse? Edit = null);

/// <summary>Bounded, account/backend/project-isolated preview assets. Only a project metadata save promotes a concept.</summary>
public sealed partial class LocalCoverStudioStore(string root)
{
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow, MaxDepth=16 };
    private readonly SemaphoreSlim gate = new(1,1);
    private string PathFor(string scope, Guid document) {
        if(scope.Length != 64 || !scope.All(Uri.IsHexDigit) || document==Guid.Empty) throw new InvalidDataException("Invalid cover cache identity.");
        return Path.Combine(root,scope,document.ToString("N")+".json");
    }
    public static void Validate(LocalCoverDraft draft) {
        if(draft.Version is not (1 or 2) || draft.Id==Guid.Empty || draft.DocumentId==Guid.Empty || draft.ProjectId==Guid.Empty
            || draft.CloudDocumentId==Guid.Empty || draft.CloudProjectId==Guid.Empty || draft.SourceRevision<0 || draft.MetadataRevision<0
            || draft.CloudMetadataRevision<0 || string.IsNullOrWhiteSpace(draft.DocumentVersion) || draft.DocumentVersion.Length>64
            || draft.Images is null || draft.Images.Count is <1 or >4 || draft.Selected<0 || draft.Selected>=draft.Images.Count)
            throw new InvalidDataException("Invalid cover preview. Existing covers are preserved.");
        foreach(string image in draft.Images) CoverStudioContract.ReadPng(image);
        if(draft.Edit is { } edit) {
            CoverEdits.ValidateResponse(edit.Request,edit);
            if(draft.Images.Count!=2 || draft.Assets is not {Count:2} || draft.Assets[0]?.AssetId!=edit.Request.AssetId
                || CoverAssetContract.Hash(CoverStudioContract.ReadPng(draft.Images[0]))!=edit.Request.ContentHash
                || draft.Images[1]!="data:image/png;base64,"+Convert.ToBase64String(edit.Proposed.Bytes)
                || draft.Assets[1]!=edit.Proposed.Asset || draft.CloudDocumentId!=edit.Request.Source.DocumentId
                || draft.CloudProjectId!=edit.Request.Source.ProjectId || draft.CloudMetadataRevision!=edit.Request.Source.MetadataRevision
                || draft.DocumentVersion!=edit.Request.Source.DocumentVersion)throw new InvalidDataException("The cover adjustment review is incomplete or mismatched.");
        }
        if(draft.Assets is not null) {
            if(draft.Assets.Count!=draft.Images.Count)throw new InvalidDataException("Cover asset mapping is incomplete.");
            for(int i=0;i<draft.Images.Count;i++)if(draft.Assets[i] is { } asset)CoverAssetContract.Validate(asset,CoverStudioContract.ReadPng(draft.Images[i]),draft.CloudProjectId);
        }
    }
    public async Task<LocalCoverDraft?> ReadAsync(string scope, LocalDocument doc, CancellationToken ct=default) {
        string path=PathFor(scope,doc.DocumentId); if(!File.Exists(path)) return null;
        if(new FileInfo(path).Length>16*1024*1024) throw new InvalidDataException("Cover cache exceeds the safe limit.");
        var draft=JsonSerializer.Deserialize<LocalCoverDraft>(await File.ReadAllBytesAsync(path,ct),Json) ?? throw new InvalidDataException("Invalid cover cache.");
        Validate(draft);
        if(draft.Scope!=scope || draft.DocumentId!=doc.DocumentId) throw new InvalidDataException("Cover preview belongs to another account or document.");
        if(draft.ProjectId!=doc.Project?.ProjectId || draft.CloudDocumentId!=doc.ServerDocumentId || draft.CloudProjectId!=doc.Project.ServerProjectId) return null;
        return draft;
    }
    public async Task WriteAsync(LocalCoverDraft draft, CancellationToken ct=default) {
        Validate(draft); var path=PathFor(draft.Scope,draft.DocumentId); await gate.WaitAsync(ct);
        try { Directory.CreateDirectory(Path.GetDirectoryName(path)!); await new AtomicDocumentWriter().WriteAsync(path,JsonSerializer.SerializeToUtf8Bytes(draft,Json),ct); }
        finally { gate.Release(); }
    }
}
