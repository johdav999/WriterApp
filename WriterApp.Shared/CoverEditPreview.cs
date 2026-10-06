namespace WriterApp.Shared;

public sealed record CoverEditPreview(int Version, string Scope, CoverEditResponse Edit, string Original, int Selected, Guid OperationId, bool SaveApproved = false, CoverEditSaveReceipt? Receipt = null, CoverAssetIdentity? OriginalAsset = null);
public static class CoverEditPreviews
{
    public static void Validate(CoverEditPreview preview,string scope,Guid project) {
        if(preview is null || preview.Version!=1 || preview.Scope!=scope || scope.Length!=64 || preview.Selected is <0 or >1 || preview.OperationId==Guid.Empty
            || preview.Edit?.Request.Source.ProjectId!=project)throw new InvalidDataException("The cached cover review belongs to another account/backend/project or is malformed.");
        CoverEdits.ValidateResponse(preview.Edit.Request,preview.Edit);
        if(CoverAssetContract.Hash(CoverStudioContract.ReadPng(preview.Original))!=preview.Edit.Request.ContentHash)throw new InvalidDataException("Original cover cache hash changed.");
        if(preview.OriginalAsset is null || preview.OriginalAsset.AssetId!=preview.Edit.Request.AssetId || preview.OriginalAsset.ContentHash!=preview.Edit.Request.ContentHash)
            throw new InvalidDataException("Original owned asset identity is missing.");
        CoverAssetContract.Validate(preview.OriginalAsset,CoverStudioContract.ReadPng(preview.Original),project);
        if(preview.Receipt is { } receipt && (receipt.Version!=1 || receipt.OperationId!=preview.OperationId || receipt.ProjectId!=project || receipt.State is not ("approved" or "committed" or "restored")))
            throw new InvalidDataException("Cover save receipt does not match its approval.");
    }
}
