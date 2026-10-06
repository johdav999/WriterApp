using System.Data;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data.Documents;
using WriterApp.Shared;

namespace WriterApp.Application.Covers;

public sealed partial class CoverAssetService
{
    public async Task<CoverAssetResponse> RegisterInlineAsync(string owner,CoverAssetSource source,byte[] bytes,CancellationToken ct)
    {
        CoverAssetContract.ValidateSource(source); CoverAssetContract.ValidateRemotePng(bytes);
        string hash=CoverAssetContract.Hash(bytes), reference="urn:writerapp:cover:"+hash;
        string key=CoverAssetContract.ReferenceHash(reference+"|"+source.DocumentId+"|"+source.MetadataRevision+"|"+source.DocumentVersion);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            await db.Database.ExecuteSqlRawAsync("UPDATE DocumentSyncClocks SET Sequence=Sequence WHERE Id=1",ct);
            if(!await CurrentAsync(owner,source,ct))throw Stale();
            var record=await db.CoverAssets.SingleOrDefaultAsync(a=>a.OwnerUserId==owner && a.ProjectId==source.ProjectId && a.ReferenceHash==key,ct);
            if(record is null) {
                record=new(){Version=2,Id=Guid.NewGuid(),OwnerUserId=owner,ProjectId=source.ProjectId,SourceDocumentId=source.DocumentId,
                    SourceMetadataRevision=source.MetadataRevision,SourceDocumentVersion=source.DocumentVersion,ReferenceHash=key,
                    RemoteReference=reference,ContentHash=hash,Bytes=bytes};
                db.CoverAssets.Add(record); await db.SaveChangesAsync(ct);
            }
            var asset=Identity(record); CoverAssetContract.Validate(asset,record.Bytes,source.ProjectId);
            await transaction.CommitAsync(ct); return new CoverAssetResponse(source,asset,record.Bytes);
        });
    }
    private static CoverAssetIdentity Identity(CoverAssetRecord record)=>new(record.Version,record.Id,record.ProjectId,record.SourceDocumentId,
        record.SourceMetadataRevision,record.SourceDocumentVersion,record.RemoteReference,record.ContentHash,record.MediaType,record.Bytes.Length);

    public async Task<CoverAssetResponse> EditSourceAsync(string owner,CoverEditSourceRequest input,CancellationToken ct)
    {
        if(input.ProjectId==Guid.Empty || input.DocumentId==Guid.Empty || input.MetadataRevision<0 || input.ReferenceHash is not {Length:64})
            throw new InvalidDataException("Invalid cover source selection.");
        var state=await db.DocumentSyncRecords.AsNoTracking().SingleOrDefaultAsync(r=>r.DocumentId==input.DocumentId && r.OwnerUserId==owner,ct);
        if(state is null)throw new CoverAssetException(404,"cover.not_found","The owned saved document is unavailable.");
        var source=new CoverAssetSource(1,input.ProjectId,input.DocumentId,input.MetadataRevision,state.Version,input.ReferenceHash);
        if(!await CurrentAsync(owner,source,ct))throw Stale();
        if(input.AssetId is null) return await ProjectAsync(owner,source,ct);
        var record=await db.CoverAssets.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==input.AssetId && a.OwnerUserId==owner && a.ProjectId==input.ProjectId && a.SourceDocumentId==input.DocumentId,ct)
            ?? throw new CoverAssetException(404,"cover.not_found","The owned selected cover asset was deleted or is unavailable. Regenerate or reopen the saved cover.");
        if(CoverAssetContract.ReferenceHash(record.RemoteReference)!=input.ReferenceHash)throw Stale();
        var asset=Identity(record);CoverAssetContract.Validate(asset,record.Bytes,input.ProjectId);
        return new(source,asset,record.Bytes);
    }
    public async Task<CoverAssetResponse> RequireEditSourceAsync(string owner,CoverEditRequest request,CancellationToken ct)
    {
        CoverEdits.Validate(request);
        var resolved=await EditSourceAsync(owner,new(request.Source.ProjectId,request.Source.DocumentId,request.Source.MetadataRevision,request.AssetId,request.Source.ReferenceHash),ct);
        if(resolved.Source!=request.Source || resolved.Asset.ContentHash!=request.ContentHash)throw Stale();
        return resolved;
    }
}
