using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Shared;
using Microsoft.EntityFrameworkCore;
using WriterApp.Controllers;

namespace WriterApp.Application.AI;

/// <summary>Additive CAS for explicit web AI planning approvals; legacy saves omit the header.</summary>
public sealed class WebAiMutationFilter(AppDbContext db,IUserIdResolver users) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next)
    {
        var header=context.HttpContext.Request.Headers["X-WriterApp-AI-Source"].ToString();
        string? expectedCard=context.ActionArguments.Values.OfType<SceneCardUpdateRequest>().FirstOrDefault()?.ExpectedCardFingerprint
            ?? context.ActionArguments.Values.OfType<SectionSceneCardUpdateRequest>().FirstOrDefault()?.ExpectedCardFingerprint;
        var approved=context.ActionArguments.Values.OfType<SceneCardUpdateRequest>().FirstOrDefault()?.ApprovedFields
            ?? context.ActionArguments.Values.OfType<SectionSceneCardUpdateRequest>().FirstOrDefault()?.ApprovedFields;
        if (approved is not null && (header.Length==0 || expectedCard is null)) {
            context.Result=new BadRequestObjectResult(new { message="Partial scene approval requires a checked source and saved card." });return;
        }
        var operationHeader=context.HttpContext.Request.Headers["X-WriterApp-AI-Operation"].ToString();
        if (approved is not null && operationHeader.Length == 0) {
            context.Result=new BadRequestObjectResult(new { message="Partial scene approval requires a durable reviewed intent." });return;
        }
        if(header.Length==0 && expectedCard is null && operationHeader.Length==0) { await next();return; }
        if(header.Length>4096) { context.Result=new BadRequestObjectResult(new { message="Invalid AI approval source." });return; }
        try {
            WebAiSource? source=header.Length==0 ? null : JsonSerializer.Deserialize<WebAiSource>(header) ?? throw new InvalidDataException("Missing AI approval source.");
            if(source is not null)WebAiSources.Validate(source);
            if(expectedCard is not null && (expectedCard.Length!=64 || !expectedCard.All(Uri.IsHexDigit)))throw new InvalidDataException("Invalid saved scene source. Reload planning.");
            string method=(context.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)?.MethodInfo.Name ?? "";
            bool allowed=context.Controller switch {
                PagesController=>method=="UpdatePage", ProjectSceneContentController=>method=="Put",
                SceneCardsController=>method=="Put",SectionSceneCardsController=>method=="UpdateSceneCard",
                DocumentSynopsisController=>method=="UpdateSynopsis",ProjectsController=>method=="CreateNode",
                AiActionsController=>method is "Undo" or "Redo",_=>false
            };
            if(!allowed)throw new InvalidDataException("AI approval headers are supported only for scoped saves, not generation.");
            var args=context.ActionArguments;
            if(source is not null && (args.TryGetValue("documentId",out var document) && document is Guid did && did!=source.DocumentId
                || args.TryGetValue("projectId",out var project) && project is Guid pid && pid!=source.ProjectId
                || args.TryGetValue("sceneNodeId",out var scene) && scene is Guid sid && sid!=source.SceneId
                || args.TryGetValue("sectionId",out var section) && section is Guid sec && source.SectionId!=sec
                || args.TryGetValue("nodeId",out var node) && node is Guid nid && source.SceneId!=nid))
                throw new InvalidDataException("Approval belongs to another planning target.");
            if(source is not null && args.TryGetValue("pageId",out var page) && page is Guid pageId && source.PageId!=pageId)throw new InvalidDataException("Approval belongs to another writing page.");
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
                db.ChangeTracker.Clear();
                await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,context.HttpContext.RequestAborted);
                await db.Database.ExecuteSqlRawAsync("UPDATE DocumentSyncClocks SET Sequence=Sequence WHERE Id=1",context.HttpContext.RequestAborted);
                WriterApp.Data.AI.WebAiHistoryOperationRecord? operation=null;
                WebRecoverySnapshot? recoveryBefore=null;
                var history=new WebAiHistoryOperations(db,users.ResolveUserId(context.HttpContext.User),context.HttpContext.RequestServices,context.HttpContext);
                if(operationHeader.Length>0) {
                    if(source is null || !Guid.TryParse(operationHeader,out var operationId))throw new InvalidDataException("Invalid durable AI save identity.");
                    operation=await db.WebAiHistoryOperations.SingleOrDefaultAsync(o=>o.OwnerUserId==users.ResolveUserId(context.HttpContext.User) && o.OperationId==operationId,context.HttpContext.RequestAborted)
                        ?? throw new InvalidDataException("Durable AI save intent was not prepared.");
                    var intent=WebAiHistoryOperations.Intent(operation);
                    if(intent.Source!=source || intent.TargetKind=="Page" && context.Controller is not PagesController
                        || intent.TargetKind=="SceneContent" && context.Controller is not ProjectSceneContentController
                        || intent.TargetKind=="SceneCard" && context.Controller is not SceneCardsController || intent.TargetKind=="SectionCard" && context.Controller is not SectionSceneCardsController
                        || intent.TargetKind=="Synopsis" && context.Controller is not DocumentSynopsisController || intent.TargetKind=="Aggregate")throw new InvalidDataException("History operation targets another save.");
                    string? submitted=context.ActionArguments.Values.OfType<PageUpdateRequest>().FirstOrDefault()?.Content
                        ?? context.ActionArguments.Values.OfType<SceneContentUpdateRequest>().FirstOrDefault()?.ContentJson;
                    if(intent.TargetKind is "SceneCard" or "SectionCard")submitted=JsonSerializer.Serialize(context.ActionArguments.Values.First(v=>v is SceneCardUpdateRequest or SectionSceneCardUpdateRequest),WebAiHistoryContracts.Json);
                    if(intent.TargetKind=="Synopsis")submitted=JsonSerializer.Serialize(context.ActionArguments.Values.OfType<WriterApp.Application.Synopsis.DocumentSynopsisDto>().Single(),WebAiHistoryContracts.Json);
                    if(submitted!=intent.AfterContent)throw new InvalidDataException("Saved content differs from the approved immutable history intent.");
                    await history.RequireOwner(operation.DocumentId,context.HttpContext.RequestAborted);
                    if(operation.CommittedAt is not null) {
                        if(intent.TargetKind is "SceneCard" or "SectionCard")throw new InvalidOperationException("Planning Save committed earlier. Reload current planning and reconcile its history receipt; no repeated card write is performed.");
                        if(await history.Content(intent,context.HttpContext.RequestAborted)!=intent.AfterContent)
                            throw new InvalidOperationException("This operation committed earlier, but writing changed afterward. Reload current writing; history delivery can still reconcile its saved receipt.");
                        context.Result=new ContentResult {Content=operation.SavedResponseJson,ContentType="application/json",StatusCode=200};
                        context.HttpContext.Response.Headers["X-WriterApp-Checked-Save"]="1";
                        context.HttpContext.Response.Headers["X-WriterApp-AI-Operation"]=operation.OperationId.ToString();
                        return;
                    }
                    if(await history.Content(intent,context.HttpContext.RequestAborted)!=intent.BeforeContent)throw new InvalidOperationException("Writing changed before the prepared AI save.");
                    if(WebPlanningRecovery.Supports(intent.TargetKind))recoveryBefore=await new WebPlanningRecovery(db).Capture(intent,context.HttpContext.RequestAborted);
                }
                if(source is not null)await new WebAiSourceService(db,users.ResolveUserId(context.HttpContext.User)).Require(source,context.HttpContext.RequestAborted);
                if(expectedCard is not null) {
                    string? current=null;
                    if(context.Controller is SceneCardsController scenes && args.TryGetValue("sceneNodeId",out var sceneTarget) && sceneTarget is Guid sceneId) {
                        var card=await scenes.Get(sceneId,context.HttpContext.RequestAborted);
                        if(card.Result is OkObjectResult { Value:SceneCardDto dto })current=WebSceneCardSources.Fingerprint(dto);
                    }
                    if(context.Controller is SectionSceneCardsController sections && args.TryGetValue("sectionId",out var sectionTarget) && sectionTarget is Guid sectionId) {
                        var card=await sections.GetSceneCard(sectionId,context.HttpContext.RequestAborted);
                        if(card.Result is OkObjectResult { Value:SectionSceneCardDto dto })current=WebSceneCardSources.Fingerprint(dto);
                    }
                    if(current!=expectedCard)throw new InvalidOperationException("Scene planning changed concurrently. Reload before saving your local changes.");
                }
                if (approved is not null) {
                    var request=context.ActionArguments.Values.First(v=>v is SceneCardUpdateRequest or SectionSceneCardUpdateRequest);
                    var proposed=JsonSerializer.Deserialize<SectionSceneCardProposalDto>(JsonSerializer.Serialize(request,WebAiHistoryContracts.Json),WebAiHistoryContracts.Json)!;
                    string current;
                    if(context.Controller is SceneCardsController scenes) {
                        var read=await scenes.Get((Guid)args["sceneNodeId"]!,context.HttpContext.RequestAborted);
                        current=JsonSerializer.Serialize((read.Result as OkObjectResult)?.Value,WebAiHistoryContracts.Json);
                    } else {
                        var read=await ((SectionSceneCardsController)context.Controller).GetSceneCard((Guid)args["sectionId"]!,context.HttpContext.RequestAborted);
                        current=JsonSerializer.Serialize((read.Result as OkObjectResult)?.Value,WebAiHistoryContracts.Json);
                    }
                    var before=JsonSerializer.Deserialize<SectionSceneCardProposalDto>(current,WebAiHistoryContracts.Json)!;
                    SceneCardApprovals.Require(approved,SceneCardApprovals.Changes(before,proposed));
                }
                var result=await next();
                if(result.Exception is null && (result.Result as Microsoft.AspNetCore.Mvc.Infrastructure.IStatusCodeActionResult)?.StatusCode is not >=400)
                {
                    if(operation is not null) {
                        if(recoveryBefore is not null) {
                            var snapshot=await new WebPlanningRecovery(db).Complete(recoveryBefore,context.HttpContext.RequestAborted);
                            operation.RecoveryJson=snapshot is null?null:WebPlanningRecovery.Serialize(snapshot);
                        }
                        if(WebAiHistoryOperations.Intent(operation).TargetKind is "Page" or "SceneContent" && await history.Content(WebAiHistoryOperations.Intent(operation),context.HttpContext.RequestAborted)!=WebAiHistoryOperations.Intent(operation).AfterContent)
                            throw new InvalidDataException("Content was not persisted as approved; the transaction is rolled back.");
                        operation.CommittedAt=DateTimeOffset.UtcNow;
                        operation.SavedResponseJson=JsonSerializer.Serialize((result.Result as ObjectResult)?.Value,WebAiHistoryContracts.Json);
                        await db.SaveChangesAsync(context.HttpContext.RequestAborted);
                        context.HttpContext.Response.Headers["X-WriterApp-AI-Operation"]=operation.OperationId.ToString();
                    }
                    await tx.CommitAsync(context.HttpContext.RequestAborted);
                    context.HttpContext.Response.Headers["X-WriterApp-Checked-Save"]="1";
                    if(context.HttpContext.Items.Remove("WebAiCommittedStructureCommand",out var committed) && committed is WriterApp.Application.Commands.IStructureUndoCommand command)
                        (context.HttpContext.RequestServices.GetService(typeof(WriterApp.Application.Commands.IStructureCommandProcessor)) as WriterApp.Application.Commands.IStructureCommandProcessor)?.RecordCommitted(command);
                }
            });
        } catch(Exception e) when(e is DocumentSyncException or InvalidDataException or InvalidOperationException or JsonException) {
            context.Result=new ConflictObjectResult(new { code="ai.approval_changed",message=e.Message });
        } catch(DbUpdateException) {
            context.Result=new ObjectResult(new {code="ai.save_failed",message="Checked save and recovery history were rolled back. Retain the draft and inspect the unchanged operation ID before retrying."}){StatusCode=503};
        }
    }
}
