using System.Data;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Shared;

namespace WriterApp.Application.Covers;

public sealed partial class CoverAssetService(AppDbContext db,TrustedCoverFetcher fetcher)
{
    public async Task<bool> CurrentAsync(string owner,CoverAssetSource source,CancellationToken ct) {
        if(await db.DeletedUserIdentities.AsNoTracking().AnyAsync(x=>x.UserId==owner,ct))return false;
        return await db.Projects.AsNoTracking().AnyAsync(p=>p.Id==source.ProjectId && p.OwnerUserId==owner && p.MetadataRevision==source.MetadataRevision,ct)
            && await db.Documents.AsNoTracking().AnyAsync(d=>d.Id==source.DocumentId && d.ProjectId==source.ProjectId && d.OwnerUserId==owner && d.DeletedAtUtc==null && !d.IsArchived,ct)
            && await db.DocumentSyncRecords.AsNoTracking().AnyAsync(r=>r.DocumentId==source.DocumentId && r.OwnerUserId==owner && r.Version==source.DocumentVersion && !r.IsDeleted && !r.IsTrashed,ct);
    }
    private static CoverAssetException Stale() => new(409,"cover.stale_source","The owned cover/project/document changed. Synchronize and reopen; the saved cover and cache are preserved.");
    public async Task<CoverAssetSource> GenerationSourceAsync(string owner,CoverPrompt prompt,string reference,CancellationToken ct) {
        // A legacy web request must still name an owned current project before provider output can be fetched.
        var project=await db.Projects.AsNoTracking().SingleOrDefaultAsync(p=>p.Id==prompt.ProjectId && p.OwnerUserId==owner,ct);
        if(project is null || prompt.ExpectedMetadataRevision!=project.MetadataRevision)throw Stale();
        Guid? document=prompt.DocumentId ?? project.PrimaryDocumentId;
        var state=await db.DocumentSyncRecords.AsNoTracking().SingleOrDefaultAsync(r=>r.DocumentId==document && r.OwnerUserId==owner,ct);
        if(document is null || state is null)throw Stale();
        var source=new CoverAssetSource(1,project.Id,document.Value,project.MetadataRevision,prompt.ExpectedDocumentVersion ?? state.Version,CoverAssetContract.ReferenceHash(reference));
        if(!await CurrentAsync(owner,source,ct))throw Stale();return source;
    }
    public async Task<CoverAssetResponse> ProjectAsync(string owner,CoverAssetSource source,CancellationToken ct) {
        CoverAssetContract.ValidateSource(source);
        var project=await db.Projects.AsNoTracking().SingleOrDefaultAsync(p=>p.Id==source.ProjectId && p.OwnerUserId==owner,ct)
            ?? throw new CoverAssetException(404,"cover.not_found","The owned project cover is unavailable.");
        string reference=project.CoverImageUrl ?? throw new CoverAssetException(404,"cover.not_found","No project cover is saved.");
        if(CoverAssetContract.ReferenceHash(reference)!=source.ReferenceHash || !await CurrentAsync(owner,source,ct))throw Stale();
        if(reference.StartsWith("data:image/png;base64,",StringComparison.Ordinal))
            return await RegisterInlineAsync(owner,source,CoverStudioContract.ReadPng(reference),ct);
        return await MaterializeAsync(owner,source,reference,true,ct);
    }
    public async Task<CoverAssetResponse> MaterializeAsync(string owner,CoverAssetSource source,string reference,bool projectCover,CancellationToken ct) {
        CoverAssetContract.ValidateSource(source);
        if(CoverAssetContract.ReferenceHash(reference)!=source.ReferenceHash || !await CurrentAsync(owner,source,ct))throw Stale();
        if(await db.CoverAssets.AsNoTracking().AnyAsync(a=>a.ReferenceHash==source.ReferenceHash && (a.OwnerUserId!=owner || a.ProjectId!=source.ProjectId),ct))
            throw new CoverAssetException(404,"cover.not_found","This reference is registered to another owned project. Existing cover/cache is preserved.");
        // Only this internal call accepts a reference: owned stored metadata or the server provider result.
        fetcher.ValidateReference(reference);
        var retained=await db.CoverAssets.AsNoTracking().SingleOrDefaultAsync(a=>a.OwnerUserId==owner && a.ProjectId==source.ProjectId && a.ReferenceHash==source.ReferenceHash,ct);
        byte[] bytes=retained?.Bytes ?? await fetcher.FetchAsync(reference,ct);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            await db.Database.ExecuteSqlRawAsync("UPDATE DocumentSyncClocks SET Sequence=Sequence WHERE Id=1",ct);
            if(!await CurrentAsync(owner,source,ct) || projectCover && !await db.Projects.AsNoTracking().AnyAsync(p=>p.Id==source.ProjectId && p.OwnerUserId==owner && p.CoverImageUrl==reference,ct))throw Stale();
            if(await db.CoverAssets.AsNoTracking().AnyAsync(a=>a.ReferenceHash==source.ReferenceHash && (a.OwnerUserId!=owner || a.ProjectId!=source.ProjectId),ct))
                throw new CoverAssetException(404,"cover.not_found","This reference belongs to another owned project. Existing cover/cache is preserved.");
            var record=await db.CoverAssets.AsNoTracking().SingleOrDefaultAsync(a=>a.OwnerUserId==owner && a.ProjectId==source.ProjectId && a.ReferenceHash==source.ReferenceHash,ct);
            if(record is null) {
                CoverAssetContract.ValidateRemotePng(bytes);
                record=new(){Id=Guid.NewGuid(),OwnerUserId=owner,ProjectId=source.ProjectId,SourceDocumentId=source.DocumentId,
                    SourceMetadataRevision=source.MetadataRevision,SourceDocumentVersion=source.DocumentVersion,ReferenceHash=source.ReferenceHash,
                    RemoteReference=reference,ContentHash=CoverAssetContract.Hash(bytes),Bytes=bytes};
                db.CoverAssets.Add(record);await db.SaveChangesAsync(ct);
            }
            var identity=new CoverAssetIdentity(record.Version,record.Id,record.ProjectId,record.SourceDocumentId,record.SourceMetadataRevision,
                record.SourceDocumentVersion,record.RemoteReference,record.ContentHash,record.MediaType,record.Bytes.Length);
            if(record.RemoteReference!=reference)throw new InvalidDataException("Cached owned cover provenance changed. Existing bytes are preserved.");
            CoverAssetContract.Validate(identity,record.Bytes,source.ProjectId);
            await transaction.CommitAsync(ct);return new CoverAssetResponse(source,identity,record.Bytes);
        });
    }
}
