using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Shared;
using System.Text.Json;

namespace WriterApp.Tests;
public sealed partial class CollaborationSessionTests
{
    // Deterministic generation only. Approval, source checks, persistence, receipts and
    // history below continue through production controllers and the real collaboration worker.
    private static void MapBrowserAiFixture(WebApplication app)
    {
        app.MapGet("/__probe/outline/{document:guid}",async(HttpContext context,Guid document,AppDbContext db,CollaborationAccessService access)=>
            Results.Ok(await CollaborationWritingOutline.Read(db,access,context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value,document,context.RequestAborted)));
        app.MapPost("/__probe/ai",async(HttpContext context,AiActionExecuteRequestDto request,AppDbContext db,CollaborationAccessService access)=> {
            string actor=context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            await new WebAiSourceService(db,actor,access).Require(request.WebSource!,context.RequestAborted);
            string key=context.Request.Query["action"].FirstOrDefault() ?? "rewrite.selection";
            string proposed="REVIEWED-TEXT";
            if(key=="expand.section") {
                var original=WritingActions.Parse(request.Parameters![WritingActions.Parameter]!.ToString()!);
                proposed=WritingActions.Serialize(original with {Pages=original.Pages.Select(p=>p with {Runs=p.Runs.Select(r=>r with {Text=System.Text.RegularExpressions.Regex.Replace(r.Text,@"\S(?=\s*$)","$0 SECTION-APPROVAL")}).ToArray()}).ToArray()});
            }
            var result=new AiActionExecuteResponseDto(Guid.NewGuid(),request.OriginalText,proposed,"Fixture proposal",DateTimeOffset.UtcNow,key,SourceDocumentVersion:request.ExpectedDocumentVersion,SourceOutlineFingerprint:request.WritingOutline?.Fingerprint,WebSource:request.WebSource);
            db.AiActionHistoryEntries.Add(new(){Id=result.ProposalId,OwnerUserId=actor,DocumentId=request.DocumentId,SectionId=request.SectionId,PageId=request.PageId,
                ActionKey=result.ActionKey,CreatedAt=result.CreatedUtc,RequestJson=JsonSerializer.Serialize(request,WebAiHistoryContracts.Json),ResultJson=JsonSerializer.Serialize(result,WebAiHistoryContracts.Json)});
            await db.SaveChangesAsync();return Results.Ok(result);
        });
        app.MapGet("/__probe/history",async(HttpContext context,Guid documentId,AppDbContext db)=> {
            string actor=context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            var entries=await new EfCoreAiActionHistoryStore(db).ListAsync(actor,documentId,default);
            return Results.Ok(entries.Select(e=>new AiActionHistoryEntryDto(e.ProposalId,e.ActionKey,e.Summary,e.OriginalText,e.ProposedText,e.CreatedUtc,e.IsApplied,e.LastAppliedAt,e.AppliedCount,
                e.IsApplied?AiCommandStatusDto.Applied:AiCommandStatusDto.Succeeded,e.CanCloudUndo,e.CanCloudRedo,e.ReplayScope)));
        });
    }
    private sealed class BrowserPlans:IEntitlementService
    {
        public Task<UserEntitlements> GetEntitlementsAsync(string actor)=>Task.FromResult(new UserEntitlements(actor,"professional","professional",new Dictionary<string,string>()));
        public PlanTier GetUserTier(UserEntitlements value)=>PlanTier.Professional;
        public Task<bool> HasAsync(string actor,string key)=>Task.FromResult(true);
        public Task<int?> GetIntAsync(string actor,string key)=>Task.FromResult<int?>(null);
        public void InvalidateForUser(string actor){}
    }
}
