using System.Data;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data.AI;
using WriterApp.Shared;

namespace WriterApp.Controllers;

public sealed partial class AiPresetsController
{
    private static string Revision(PromptPresetRecord p)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ReusablePrompts.Canonical(p))));
    private static PromptPresetDto TransferMap(PromptPresetRecord p) {
        if(p.ParametersJson is null||p.ParametersJson.Length>20_000)throw new InvalidDataException("Stored preset parameters exceed the transfer limit.");
        var dto=new PromptPresetDto(p.Id,p.ProjectId,p.Name,p.Category,p.Kind,p.BuiltinActionId,p.TemplateText,
            JsonSerializer.Deserialize<Dictionary<string,object?>>(p.ParametersJson,JsonOptions)??throw new InvalidDataException("Missing stored preset parameters."),p.CreatedUtc,p.UpdatedUtc,
            p.Scope is null?null:(WritingScope)p.Scope.Value,p.Pinned);
        ReusablePrompts.ValidateEnvelope(ReusablePrompts.FromCloud(dto));return dto with{Version=Revision(p)};
    }

    [HttpGet("transfer-library")]
    public async Task<ActionResult<IReadOnlyList<PromptPresetDto>>> TransferLibrary(CancellationToken ct) {
        string owner;try{owner=_userIdResolver.ResolveUserId(User);}catch(SecurityException){return Unauthorized();}
        var values=new List<PromptPresetDto>();
        try {
            await foreach(var preset in _dbContext.PromptPresets.AsNoTracking().Where(p=>p.OwnerUserId==owner).OrderBy(p=>p.Name).Take(501).AsAsyncEnumerable().WithCancellation(ct)) {
                if(values.Count==500)return BadRequest(new{code="preset.limit",message="Cloud transfers support at most 500 presets."});values.Add(TransferMap(preset));
            }return Ok(values.ToArray());
        }catch(Exception e)when(e is JsonException or InvalidDataException){return BadRequest(new{code="preset.invalid",message="A stored preset exceeds the transfer contract or contains malformed parameters. Its original is retained."});}
    }
    [HttpPost("transfer")]
    public async Task<ActionResult<PromptTransferResponse>> Transfer([FromBody]PromptTransferRequest request,CancellationToken ct) {
        string owner;try{owner=_userIdResolver.ResolveUserId(User);}catch(SecurityException){return Unauthorized();}
        try {
            if(request is null||request.Version!=1||request.OperationId==Guid.Empty||request.PresetId==Guid.Empty||request.ExpectedVersion?.Length>200
                ||request.Action is not ("upsert" or "delete")||request.Action=="delete"&&(request.Preset is not null||string.IsNullOrWhiteSpace(request.ExpectedVersion)))
                return BadRequest(new{code="preset.invalid",message="Invalid versioned preset transfer."});
            if(request.Action=="upsert")ReusablePrompts.ValidateEnvelope(request.Preset??throw new InvalidDataException("Missing preset."));
        }catch(Exception e)when(e is InvalidDataException or JsonException){return BadRequest(new{code="preset.invalid",message=e.Message});}
        string hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ReusablePrompts.Canonical(request))));
        return await _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async()=> {
            // A provider retry must re-read committed versions, not replay tracked entities from a rolled-back attempt.
            foreach(var tracked in _dbContext.ChangeTracker.Entries().Where(e=>e.Entity is PromptPresetRecord or PromptPresetTransferRecord).ToArray())tracked.State=EntityState.Detached;
            await using var transaction=await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            var receipt=await _dbContext.PromptPresetTransfers.AsNoTracking().SingleOrDefaultAsync(r=>r.OwnerUserId==owner&&r.OperationId==request.OperationId,ct);
            if(receipt is not null) {
                if(receipt.RequestHash!=hash)return (ActionResult<PromptTransferResponse>)Conflict(new{code="preset.operation_reused",message="This operation already has different authored content."});
                var ack=JsonSerializer.Deserialize<PromptTransferResponse>(receipt.ResultJson,JsonOptions)!;await transaction.CommitAsync(ct);return Ok(ack);
            }
            var current=await _dbContext.PromptPresets.SingleOrDefaultAsync(p=>p.Id==request.PresetId,ct);
            if(current is not null&&current.OwnerUserId!=owner)return (ActionResult<PromptTransferResponse>)NotFound();
            if(current is null ? request.ExpectedVersion is not null : Revision(current)!=request.ExpectedVersion)
                return (ActionResult<PromptTransferResponse>)Conflict(new PromptTransferConflict("preset.conflict",request.PresetId,current is null?null:TransferMap(current)));
            var now=DateTimeOffset.UtcNow;
            if(request.Action=="upsert") {
                var p=request.Preset!;
                if(p.ProjectId is { } project&&!await _dbContext.Projects.AsNoTracking().AnyAsync(x=>x.Id==project&&x.OwnerUserId==owner,ct))return (ActionResult<PromptTransferResponse>)NotFound();
                if(current is null){current=new(){Id=request.PresetId,OwnerUserId=owner,CreatedUtc=now};_dbContext.PromptPresets.Add(current);}
                current.Name=p.Name;current.Category=p.Category;current.Kind=p.Kind;current.BuiltinActionId=p.ActionKey;current.TemplateText=p.Template;
                current.ParametersJson=JsonSerializer.Serialize(p.Parameters,JsonOptions);current.ProjectId=p.ProjectId;current.Scope=(int)p.Scope;current.Pinned=p.Pinned;current.UpdatedUtc=now;
            } else if(current is not null)_dbContext.PromptPresets.Remove(current);
            var result=new PromptTransferResponse(1,request.OperationId,request.PresetId,request.Action=="delete",current is null?request.ExpectedVersion:Revision(current),request.Action=="delete"?null:TransferMap(current!));
            _dbContext.PromptPresetTransfers.Add(new(){OwnerUserId=owner,OperationId=request.OperationId,RequestHash=hash,ResultJson=JsonSerializer.Serialize(result,JsonOptions),CreatedUtc=now});
            await _dbContext.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return Ok(result);
        });
    }
}
