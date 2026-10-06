using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Data.Documents;
using WriterApp.Shared;
using WriterApp.Shared.Sync;

namespace WriterApp.Controllers;

public sealed partial class OnboardingController
{
    private static readonly JsonSerializerOptions DemoJson = new(JsonSerializerDefaults.Web);
    private static string DemoHash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, DemoJson)));

    [HttpGet("demo/status")]
    public Task<IActionResult> DemoStatus([FromServices] IOnboardingDemoEligibilityService eligibility,
        [FromServices] IOptions<WriterAiOptions> options, CancellationToken ct) => DemoRun(async () =>
            await ReadDemoStatus(eligibility, options.Value.Enabled, ct));

    [HttpPost("demo/bootstrap"),RequestSizeLimit(16_384)]
    public Task<IActionResult> DemoBootstrap(OnboardingDemoBootstrapRequest request,
        [FromServices] IOnboardingDemoEligibilityService eligibility, [FromServices] IOptions<WriterAiOptions> options, CancellationToken ct) => DemoRun(async () => {
        if (request.Version != 1 || request.OperationId == Guid.Empty || request.PrimaryWritingIntent is not { Length: > 0 and <= 100 })
            throw new DocumentSyncException(400,"onboarding.invalid_request","Invalid demo bootstrap request.");
        var owner = _userIdResolver.ResolveUserId(User);
        var profile = await GetOrCreateProfileAsync(owner, ct);
        if (profile.HasCompletedOnboarding && !await _dbContext.OnboardingDemoWorkspaces.AnyAsync(x => x.OwnerUserId == owner, ct))
            throw new DocumentSyncException(409,"onboarding.complete","Onboarding is already complete. Continue local guidance or ordinary writing.");
        await _onboardingBootstrapService.CreateStarterWorkspaceForOnboardingAsync(owner,request.PrimaryWritingIntent,ct);
        return await ReadDemoStatus(eligibility,options.Value.Enabled,ct);
    });

    [HttpPost("demo/progress"),RequestSizeLimit(16_384)]
    public Task<IActionResult> DemoProgress(OnboardingDemoProgressRequest request,
        [FromServices] IOnboardingDemoEligibilityService eligibility, [FromServices] IOptions<WriterAiOptions> options, CancellationToken ct) => DemoRun(async () => {
        if(request.Version!=1 || request.OperationId==Guid.Empty || request.Step is < 0 or > 10 || request.ExpectedRevision is not { Length:64 })
            throw new DocumentSyncException(400,"onboarding.invalid_request","Invalid progress contract.");
        var owner=_userIdResolver.ResolveUserId(User);string hash=DemoHash(request);
        return await _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var transaction=await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            await _dbContext.Database.ExecuteSqlRawAsync("UPDATE DocumentSyncClocks SET Sequence=Sequence WHERE Id=1",ct);
            var receipt=await _dbContext.OnboardingProgressOperations.SingleOrDefaultAsync(x => x.OwnerUserId==owner && x.OperationId==request.OperationId,ct);
            if(receipt is not null) {
                if(receipt.RequestHash!=hash)throw new DocumentSyncException(409,"onboarding.operation_reused","This operation belongs to different progress.");
                return JsonSerializer.Deserialize<OnboardingDemoStatus>(receipt.ReceiptJson,DemoJson)!;
            }
            var current=await ReadDemoStatus(eligibility,options.Value.Enabled,ct);
            if(current.Revision!=request.ExpectedRevision)throw new DocumentSyncException(409,"onboarding.progress_changed","Web or another device changed onboarding. Refresh and merge completed progress before retrying.");
            var profile=await GetOrCreateProfileAsync(owner,ct);
            profile.OnboardingStep=Math.Max(profile.OnboardingStep,request.Complete ? 10 : request.Step);
            if(request.Complete) {
                profile.HasCompletedOnboarding=true;profile.HasOnboarded=true;
                profile.OnboardingCompletedUtc ??= DateTimeOffset.UtcNow;
            }
            profile.UpdatedUtc=DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            var updated=await ReadDemoStatus(eligibility,options.Value.Enabled,ct);
            _dbContext.OnboardingProgressOperations.Add(new(){OwnerUserId=owner,OperationId=request.OperationId,RequestHash=hash,ReceiptJson=JsonSerializer.Serialize(updated,DemoJson)});
            await _dbContext.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return updated;
        });
    });

    private async Task<OnboardingDemoStatus> ReadDemoStatus(IOnboardingDemoEligibilityService eligibility,bool enabled,CancellationToken ct) {
        var owner=_userIdResolver.ResolveUserId(User);var profile=await GetOrCreateProfileAsync(owner,ct);
        var row=await _dbContext.OnboardingDemoWorkspaces.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerUserId==owner,ct);
        var policy=row is null ? OnboardingDemoEligibilityResult.Denied("not-created")
            : await eligibility.EvaluateSectionAiDemoAsync(owner,row.DocumentId,row.SectionId,OnboardingAiDemoRequest.ActionKey,ct);
        return new(1,DemoHash(new {profile.HasCompletedOnboarding,profile.OnboardingStep,profile.PrimaryWritingIntent,profile.OnboardingCompletedUtc}),
            profile.HasCompletedOnboarding,profile.OnboardingStep,row is null ? null : new(row.ProjectId,row.DocumentId,row.SectionId,row.SceneNodeId),
            enabled && policy.IsEligible,enabled ? policy.Reason : "ai-disabled",OnboardingAiDemoRequest.ActionKey,"section",row?.RequestUsed ?? false,
            row?.ProposalId,row?.ExpiresAtUtc,true);
    }

    [HttpGet("demo/documents/changes")]
    public Task<IActionResult> DemoChanges(string? cursor,[FromServices] DocumentSyncService sync,CancellationToken ct) =>
        DemoRun(async () => await sync.OnboardingChangesAsync(_userIdResolver.ResolveUserId(User),cursor,ct));
    [HttpGet("demo/documents/{id:guid}")]
    public Task<IActionResult> DemoDownload(Guid id,[FromServices] DocumentSyncService sync,CancellationToken ct) =>
        DemoRun(async () => await sync.DownloadAsync(_userIdResolver.ResolveUserId(User),id,ct,true,true,true,true));
    [HttpPost("demo/documents/{id:guid}/operations"),RequestSizeLimit(DocumentSyncService.MaxRequestBytes)]
    public Task<IActionResult> DemoMutate(Guid id,SyncMutation request,[FromServices] DocumentSyncService sync,CancellationToken ct) =>
        DemoRun(async () => await sync.MutateAsync(_userIdResolver.ResolveUserId(User),id,request,ct,true,true,true,true));

    private async Task<IActionResult> DemoRun<T>(Func<Task<T>> action) {
        Response.Headers.CacheControl="no-store";
        if(await TryHandleDeletedIdentityAsync(_userIdResolver.ResolveUserId(User),HttpContext.RequestAborted) is { } denied)return denied;
        try{return Ok(await action());}
        catch(DocumentSyncException e){return StatusCode(e.Status,e.Error);}
        catch(OnboardingBootstrapException e){return Conflict(new {code=e.Code,message=e.Message});}
        catch(Exception e) when(e is DbException or DbUpdateException){return StatusCode(503,new{code="onboarding.storage_unavailable",message="Demo storage was not acknowledged. Update the backend migrations or retry the same operation. Your writing is preserved."});}
    }
}
