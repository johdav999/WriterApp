namespace WriterApp.Shared;

public sealed record CoverEditCapabilities(int Version, IReadOnlyList<string> Operations, string? UnavailableReason = null);
public sealed record CoverEditSourceRequest(Guid ProjectId, Guid DocumentId, long MetadataRevision, Guid? AssetId, string ReferenceHash);
public sealed record CoverEditRequest(int Version, CoverAssetSource Source, Guid AssetId, string ContentHash, string Operation, CoverPrompt Brief);
public sealed record CoverEditResponse(int Version, CoverEditRequest Request, CoverAssetResponse Proposed, Guid ProposalId);
public sealed record CoverEditSaveRequest(Guid OperationId, CoverEditResponse Edit, Guid SelectedAssetId);
public sealed record CoverEditSaveReceipt(int Version, Guid OperationId, Guid ProjectId, string State, long MetadataRevision, string? CoverImageUrl, string? PreviousCoverImageUrl);
public sealed record CoverEditRestoreRequest(long ExpectedMetadataRevision);
public sealed record CoverEditRecoveryResponse(CoverEditSaveReceipt? Receipt);

public static class CoverEdits
{
    public static readonly string[] Operations = ["variation", "darker", "brighter", "cinematic", "minimal"];
    public static string Label(string operation) => operation switch {
        "variation" => "Create variation", "darker" => "Make darker", "brighter" => "Make brighter",
        "cinematic" => "Cinematic adjustment", "minimal" => "Minimal adjustment", _ => throw new InvalidDataException("Unsupported cover operation.") };
    public static string Instruction(string operation) => operation switch {
        "variation" => "Create a visibly distinct composition variant of this cover, retaining its subject and visual identity.",
        "darker" => "Edit this cover to a visibly darker mood and lighting while retaining legibility, its subject and composition.",
        "brighter" => "Edit this cover to visibly brighter lighting and colors while retaining its subject and composition.",
        "cinematic" => "Rework this cover with cinematic composition, dramatic depth and film-like lighting while retaining its subject.",
        "minimal" => "Simplify this cover to a minimal composition with fewer visual elements, clean negative space and a clear focal subject.",
        _ => throw new InvalidDataException("Unsupported cover operation.") };
    public static void Validate(CoverEditRequest request) {
        if(request is null || request.Version!=1 || request.AssetId==Guid.Empty || request.ContentHash is not {Length:64}
            || !request.ContentHash.All(Uri.IsHexDigit) || !Operations.Contains(request.Operation) || request.Brief is null)
            throw new InvalidDataException("Invalid cover adjustment request.");
        CoverAssetContract.ValidateSource(request.Source); CoverStudioContract.ValidatePrompt(request.Brief);
    }
    public static void ValidateResponse(CoverEditRequest request, CoverEditResponse response) {
        Validate(request);
        if(response is null || response.Version!=1 || response.ProposalId==Guid.Empty || response.Request is null
            || System.Text.Json.JsonSerializer.Serialize(response.Request)!=System.Text.Json.JsonSerializer.Serialize(request)
            || response.Proposed is null || response.Proposed.Source!=request.Source)
            throw new InvalidDataException("The backend did not confirm this exact cover operation and source.");
        CoverAssetContract.Validate(response.Proposed.Asset,response.Proposed.Bytes,request.Source.ProjectId);
        if(response.Proposed.Asset.SourceDocumentId!=request.Source.DocumentId || response.Proposed.Asset.SourceMetadataRevision!=request.Source.MetadataRevision
            || response.Proposed.Asset.SourceDocumentVersion!=request.Source.DocumentVersion || response.Proposed.Asset.ContentHash==request.ContentHash)
            throw new InvalidDataException("The image edit did not return a new validated source-matched concept.");
    }
}
