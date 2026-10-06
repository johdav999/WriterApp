using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data.Documents;
using WriterApp.Shared;

namespace WriterApp.Application.Covers;

public sealed partial class CoverAssetService
{
    public async Task<CoverEditResponse> IssueEditAsync(string owner,CoverEditRequest request,CoverAssetResponse proposed,CancellationToken ct)=>await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>{
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await db.Database.ExecuteSqlRawAsync("UPDATE DocumentSyncClocks SET Sequence=Sequence WHERE Id=1",ct);
        await RequireEditSourceAsync(owner,request,ct);
        var response=new CoverEditResponse(1,request,proposed,Guid.NewGuid());CoverEdits.ValidateResponse(request,response);
        db.CoverEditProposals.Add(new(){Id=response.ProposalId,OwnerUserId=owner,ProjectId=request.Source.ProjectId,
            ProposedAssetId=proposed.Asset.AssetId,RequestJson=JsonSerializer.Serialize(request)});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return response;
    });
    private static string SavePayload(CoverEditSaveRequest input) => JsonSerializer.Serialize(new{input.Edit.Request,Asset=input.Edit.Proposed.Asset,input.SelectedAssetId});
    private static CoverEditSaveReceipt Receipt(CoverEditSaveRecord row)=>new(1,row.Id,row.ProjectId,row.State,
        row.RestoredMetadataRevision ?? row.SavedMetadataRevision ?? row.ExpectedMetadataRevision,row.State=="restored" ? row.BeforeCover : row.AfterCover,row.BeforeCover);
    public async Task<CoverEditSaveReceipt> ReadSaveAsync(string owner,Guid operation,CancellationToken ct) {
        var row=await db.CoverEditSaves.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==operation && x.OwnerUserId==owner,ct);
        if(row is null || !await db.Projects.AnyAsync(p=>p.Id==row.ProjectId && p.OwnerUserId==owner,ct)
            || await db.DeletedUserIdentities.AnyAsync(x=>x.UserId==owner,ct))throw new CoverAssetException(404,"cover.not_found","The owned cover save receipt is unavailable.");
        return Receipt(row);
    }
    public async Task<CoverEditRecoveryResponse> LatestSaveAsync(string owner,Guid project,CancellationToken ct) {
        if(!await db.Projects.AsNoTracking().AnyAsync(p=>p.Id==project && p.OwnerUserId==owner,ct) || await db.DeletedUserIdentities.AnyAsync(x=>x.UserId==owner,ct))
            throw new CoverAssetException(404,"cover.not_found","The owned project is unavailable.");
        var row=await db.CoverEditSaves.AsNoTracking().Where(x=>x.OwnerUserId==owner && x.ProjectId==project && x.SavedMetadataRevision!=null)
            .OrderByDescending(x=>x.SavedMetadataRevision).FirstOrDefaultAsync(ct);
        return new(row is null ? null : Receipt(row));
    }
    public async Task<CoverEditSaveReceipt> SaveEditAsync(string owner,CoverEditSaveRequest input,CancellationToken ct)
    {
        if(input.OperationId==Guid.Empty || input.Edit is null)throw new InvalidDataException("Invalid cover approval identity.");
        CoverEdits.ValidateResponse(input.Edit.Request,input.Edit);
        var request=input.Edit.Request; string payload=SavePayload(input);
        var issued=await db.CoverEditProposals.AsNoTracking().SingleOrDefaultAsync(p=>p.Id==input.Edit.ProposalId && p.OwnerUserId==owner && p.ProjectId==request.Source.ProjectId,ct);
        if(issued is null || issued.Version!=1 || issued.RequestJson!=JsonSerializer.Serialize(request) || issued.ProposedAssetId!=input.Edit.Proposed.Asset.AssetId)
            throw new CoverAssetException(409,"cover.proposal_mismatch","This cover proposal is not the exact owned server-issued image edit. Regenerate before saving.");
        // Approval/recovery is committed separately before any project metadata write.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>{
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            await db.Database.ExecuteSqlRawAsync("UPDATE DocumentSyncClocks SET Sequence=Sequence WHERE Id=1",ct);
            var existing=await db.CoverEditSaves.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==input.OperationId,ct);
            if(existing is not null) {
                if(existing.OwnerUserId!=owner || existing.ProjectId!=request.Source.ProjectId || existing.PayloadJson!=payload)
                    throw new CoverAssetException(409,"cover.approval_mismatch","This operation identity belongs to a different approval.");
                await tx.CommitAsync(ct); return;
            }
            await RequireEditSourceAsync(owner,request,ct);
            var selected=await EditSourceAsync(owner,new(request.Source.ProjectId,request.Source.DocumentId,request.Source.MetadataRevision,input.SelectedAssetId,
                CoverAssetContract.ReferenceHash(input.SelectedAssetId==request.AssetId ? (await RequireEditSourceAsync(owner,request,ct)).Asset.RemoteReference : input.Edit.Proposed.Asset.RemoteReference)),ct);
            if(input.SelectedAssetId!=request.AssetId && input.SelectedAssetId!=input.Edit.Proposed.Asset.AssetId || input.SelectedAssetId==input.Edit.Proposed.Asset.AssetId && selected.Asset!=input.Edit.Proposed.Asset)
                throw new InvalidDataException("Select the original or proposed owned concept.");
            var project=await db.Projects.AsNoTracking().SingleAsync(p=>p.Id==request.Source.ProjectId && p.OwnerUserId==owner,ct);
            db.CoverEditSaves.Add(new(){Id=input.OperationId,OwnerUserId=owner,ProjectId=project.Id,PayloadJson=payload,
                ExpectedMetadataRevision=project.MetadataRevision,BeforeCover=project.CoverImageUrl,AfterCover="data:image/png;base64,"+Convert.ToBase64String(selected.Bytes)});
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        });
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>{
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            await db.Database.ExecuteSqlRawAsync("UPDATE DocumentSyncClocks SET Sequence=Sequence WHERE Id=1",ct);
            var row=await db.CoverEditSaves.SingleAsync(x=>x.Id==input.OperationId && x.OwnerUserId==owner,ct);
            if(row.State!="approved") {var retained=await ReadSaveAsync(owner,row.Id,ct);await tx.CommitAsync(ct);return retained;}
            await RequireEditSourceAsync(owner,request,ct);
            var proposed=await db.CoverAssets.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==input.Edit.Proposed.Asset.AssetId && a.OwnerUserId==owner && a.ProjectId==row.ProjectId,ct);
            if(proposed is null || proposed.ContentHash!=input.Edit.Proposed.Asset.ContentHash)throw Stale();
            int changed=await db.Projects.Where(p=>p.Id==row.ProjectId && p.OwnerUserId==owner && p.MetadataRevision==row.ExpectedMetadataRevision && p.CoverImageUrl==row.BeforeCover)
                .ExecuteUpdateAsync(set=>set.SetProperty(p=>p.CoverImageUrl,row.AfterCover).SetProperty(p=>p.MetadataRevision,row.ExpectedMetadataRevision+1).SetProperty(p=>p.UpdatedUtc,DateTimeOffset.UtcNow),ct);
            if(changed!=1)throw Stale();
            row.State="committed";row.SavedMetadataRevision=row.ExpectedMetadataRevision+1;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Receipt(row);
        });
    }
    public async Task<CoverEditSaveReceipt> RestoreEditAsync(string owner,Guid operation,long expected,CancellationToken ct)=>await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>{
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await db.Database.ExecuteSqlRawAsync("UPDATE DocumentSyncClocks SET Sequence=Sequence WHERE Id=1",ct);
        await ReadSaveAsync(owner,operation,ct);
        var row=await db.CoverEditSaves.SingleAsync(x=>x.Id==operation && x.OwnerUserId==owner,ct);
        if(row.State=="restored") {await tx.CommitAsync(ct);return Receipt(row);}
        if(row.State!="committed")throw new CoverAssetException(409,"cover.not_committed","This cover save has not committed. Reconcile or retry its approved save first.");
        int changed=await db.Projects.Where(p=>p.Id==row.ProjectId && p.OwnerUserId==owner && p.MetadataRevision==expected && p.CoverImageUrl==row.AfterCover)
            .ExecuteUpdateAsync(set=>set.SetProperty(p=>p.CoverImageUrl,row.BeforeCover).SetProperty(p=>p.MetadataRevision,expected+1).SetProperty(p=>p.UpdatedUtc,DateTimeOffset.UtcNow),ct);
        if(changed!=1)throw Stale();row.State="restored";row.RestoredMetadataRevision=expected+1;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Receipt(row);
    });
}
