using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Data.AI;
using WriterApp.Shared;

namespace WriterApp.Application.AI;

public sealed class WebAiHistoryOperations(AppDbContext db,string owner,IServiceProvider? services=null,Microsoft.AspNetCore.Http.HttpContext? context=null)
{
    public static WebAiHistoryIntent Intent(WebAiHistoryOperationRecord row) => JsonSerializer.Deserialize<WebAiHistoryIntent>(row.IntentJson,WebAiHistoryContracts.Json)!;
    public static WebAiHistoryReceipt Receipt(WebAiHistoryOperationRecord row) => new(1,row.OperationId,row.ApplicationId,row.ProposalId,
        row.Sequence,row.Outcome,row.RequestHash,row.ReportedAt is not null?"Confirmed":row.CommittedAt is not null?"Pending":"Prepared",row.CommittedAt,row.SavedResponseJson);
    public async Task RequireOwner(Guid document,CancellationToken ct)
    {
        if(!await db.Documents.AnyAsync(d=>d.Id==document && d.OwnerUserId==owner && d.DeletedAtUtc==null && !d.IsArchived,ct)
            || await db.DeletedUserIdentities.AnyAsync(u=>u.UserId==owner,ct))
            throw new InvalidOperationException("The owned manuscript/account is unavailable. Reporting intent is retained.");
    }
    public async Task<string> Content(WebAiHistoryIntent intent,CancellationToken ct)
    {
        if(intent.TargetKind=="Synopsis") {
            var s=await db.DocumentSynopses.AsNoTracking().SingleOrDefaultAsync(s=>s.DocumentId==intent.Source.DocumentId,ct);
            return JsonSerializer.Serialize(new WriterApp.Shared.Sync.SyncSynopsis(s?.Logline??"",s?.Premise??"",s?.Theme??"",s?.ProtagonistArc??"",s?.CentralConflict??"",s?.Stakes??"",s?.Setting??"",s?.EndingIntent??"",s?.OpenQuestions??"",s?.Notes??""),WebAiHistoryContracts.Json);
        }
        if(intent.TargetKind is "SceneCard" or "SectionCard") {
            if(services is null || context is null)throw new InvalidOperationException("Checked planning reader is unavailable.");
            if(intent.TargetKind=="SceneCard") {
                var controller=Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<WriterApp.Controllers.SceneCardsController>(services);
                controller.ControllerContext=new(){HttpContext=context};var result=await controller.Get(intent.Source.SceneId!.Value,ct);
                if(result.Result is Microsoft.AspNetCore.Mvc.OkObjectResult {Value:WriterApp.Application.Documents.SceneCardDto card})return WebSceneCardSources.Canonical(card);
            } else {
                var controller=Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<WriterApp.Controllers.SectionSceneCardsController>(services);
                controller.ControllerContext=new(){HttpContext=context};var result=await controller.GetSceneCard(intent.Source.SectionId!.Value,ct);
                if(result.Result is Microsoft.AspNetCore.Mvc.OkObjectResult {Value:WriterApp.Application.Documents.SectionSceneCardDto card})return WebSceneCardSources.Canonical(card);
            }
            throw new InvalidDataException("Owned planning target is unavailable.");
        }
        if(intent.TargetKind=="Page")return (await db.Pages.AsNoTracking().SingleOrDefaultAsync(p=>p.Id==intent.Source.PageId && p.DocumentId==intent.Source.DocumentId && p.SectionId==intent.Source.SectionId,ct))?.Content
            ?? throw new InvalidDataException("History page target is unavailable.");
        if(intent.TargetKind=="SceneContent")return (await db.SceneContents.AsNoTracking().SingleOrDefaultAsync(s=>s.SceneNodeId==intent.Source.SceneId,ct))?.ContentJson ?? "";
        throw new InvalidDataException("Aggregate recovery is not page HTML.");
    }
    public async Task ValidateNew(WebAiHistoryIntent intent,CancellationToken ct)
    {
        WebAiHistoryContracts.Validate(intent);await RequireOwner(intent.Source.DocumentId,ct);
        if(intent.Sequence>1 && (WebPlanningRecovery.Supports(intent.TargetKind) || intent.TargetKind=="Aggregate"))throw new InvalidDataException("Use the scoped recovery endpoint for this Undo/Redo.");
        var proposal=await db.AiActionHistoryEntries.AsNoTracking().SingleOrDefaultAsync(p=>p.Id==intent.ProposalId && p.OwnerUserId==owner,ct)
            ?? throw new InvalidDataException("Owned AI proposal not found.");
        if(proposal.DocumentId!=intent.Source.DocumentId)throw new InvalidDataException("History proposal belongs to another manuscript.");
        var result=JsonSerializer.Deserialize<AiActionExecuteResponseDto>(proposal.ResultJson,WebAiHistoryContracts.Json);
        if(result?.WebSource is not { } generated || generated.DocumentId!=intent.Source.DocumentId
            || generated.SectionId!=intent.Source.SectionId || generated.PageId!=intent.Source.PageId || generated.SceneId!=intent.Source.SceneId)
            throw new InvalidDataException("Proposal has no matching checked target.");
        if(intent.TargetKind=="Synopsis") {
            var request=JsonSerializer.Deserialize<WriterApp.Application.Synopsis.SynopsisAiRequestDto>(proposal.RequestJson,WebAiHistoryContracts.Json);
            var before=JsonSerializer.Deserialize<WriterApp.Shared.Sync.SyncSynopsis>(intent.BeforeContent!,WebAiHistoryContracts.Json)!;
            var after=JsonSerializer.Deserialize<WriterApp.Application.Synopsis.DocumentSynopsisDto>(intent.AfterContent!,WebAiHistoryContracts.Json)!;
            if(request?.FocusFieldKey is not { } field || result.ActionKey!="synopsis.story_coach" || after.DocumentId!=intent.Source.DocumentId)
                throw new InvalidDataException("Synopsis approval needs its retained field proposal.");
            var changed=typeof(WriterApp.Shared.Sync.SyncSynopsis).GetProperties().Where(p=>p.PropertyType==typeof(string) && (string?)p.GetValue(before)!=(string?)after.GetType().GetProperty(p.Name)!.GetValue(after)).ToArray();
            if(changed.Length!=1 || SynopsisCoaching.Value(before,field)!=(string?)changed[0].GetValue(before) || result.ProposedText!=(string?)after.GetType().GetProperty(changed[0].Name)!.GetValue(after)
                || !SynopsisCoaching.Fields.Any(f=>f.Key==field))
                throw new InvalidDataException("Approve exactly the generated synopsis field.");
            string property=field switch {"protagonist_arc"=>"ProtagonistArc","central_conflict"=>"CentralConflict","ending_intent"=>"EndingIntent","open_questions"=>"OpenQuestions",_=>char.ToUpperInvariant(field[0])+field[1..]};
            if(changed[0].Name!=property)throw new InvalidDataException("Another synopsis field was changed.");
        }
        if (intent.TargetKind is "SceneCard" or "SectionCard" && intent.Sequence == 1) {
            var request=JsonSerializer.Deserialize<WriterApp.Application.Documents.SceneCardUpdateRequest>(intent.AfterContent!,WebAiHistoryContracts.Json)!;
            if (request.ApprovedFields is { } fields) {
                var before=JsonSerializer.Deserialize<WriterApp.Application.Documents.SectionSceneCardProposalDto>(intent.BeforeContent!,WebAiHistoryContracts.Json)!;
                var proposed=result.ProposedSceneCard ?? throw new InvalidDataException("History needs the reviewed scene proposal.");
                WriterApp.Application.Documents.SceneCardApprovals.Require(fields,WriterApp.Application.Documents.SceneCardApprovals.Changes(before,proposed));
                var expected=WriterApp.Application.Documents.SceneCardApprovals.Request(proposed,fields,request.ExpectedCardFingerprint!);
                if(JsonSerializer.Serialize(expected,WebAiHistoryContracts.Json)!=intent.AfterContent)
                    throw new InvalidDataException("Scene approval differs from the generated fields. Review again.");
            }
        }
        var previous=await db.WebAiHistoryOperations.AsNoTracking().Where(p=>p.OwnerUserId==owner && p.ApplicationId==intent.ApplicationId)
            .OrderByDescending(p=>p.Sequence).FirstOrDefaultAsync(ct);
        if(intent.Sequence!=(previous?.Sequence ?? 0)+1 || intent.PreviousOperationId!=previous?.OperationId || previous is not null && previous.CommittedAt is null)
            throw new InvalidOperationException("Finish or inspect the preceding operation before another history transition.");
        if(previous is null) { WebAiSources.RequireTime(result.CreatedUtc);WebAiSources.RequireCurrent(generated,intent.Source); }
        else {
            var old=Intent(previous);
            if(previous.ProposalId!=intent.ProposalId || old.TargetKind!=intent.TargetKind
                || old.Source.DocumentId!=intent.Source.DocumentId || old.Source.SectionId!=intent.Source.SectionId || old.Source.PageId!=intent.Source.PageId || old.Source.SceneId!=intent.Source.SceneId
                || intent.Outcome!=(previous.Outcome=="Undone"?"Redone":"Undone") || intent.BeforeContent!=old.AfterContent || intent.AfterContent!=old.BeforeContent)
                throw new InvalidOperationException("Invalid ordered Undo/Redo transition.");
        }
        await new WebAiSourceService(db,owner).Require(intent.Source,ct);
        if(intent.TargetKind!="Aggregate" && await Content(intent,ct)!=intent.BeforeContent)throw new InvalidOperationException("Writing changed before approval. Nothing was saved.");
    }
    public async Task ReconcileAggregate(WebAiHistoryOperationRecord row,CancellationToken ct)
    {
        var intent=Intent(row);if(row.CommittedAt is not null || intent.TargetKind!="Aggregate")return;
        var aggregate=await db.WebTranslationOperations.AsNoTracking().SingleOrDefaultAsync(a=>a.OwnerUserId==owner && a.OperationId==intent.AggregateOperationId,ct);
        if(aggregate is null)return;
        var receipt=JsonSerializer.Deserialize<WebTranslationReceipt>(aggregate.ReceiptJson,WebAiHistoryContracts.Json);
        if(aggregate.ProposalId!=row.ProposalId || aggregate.DocumentId!=row.DocumentId)throw new InvalidDataException("Aggregate receipt belongs to another proposal.");
        if(receipt?.State=="Committed") {row.CommittedAt=aggregate.CreatedAt;row.SavedResponseJson=aggregate.ReceiptJson;}
    }
}
