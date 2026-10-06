using Microsoft.AspNetCore.Mvc;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.AI;
using WriterApp.Data.Documents;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class AiActionsControllerTests
{
    [Theory]
    [InlineData("current",1)]
    [InlineData("wrong-text",0)]
    [InlineData("stale-fingerprint",0)]
    [InlineData("unsupported-html",0)]
    public async Task CheckedWebTranslationValidatesSavedHtmlBeforeProviderExecution(string scenario,int calls)
    {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out var section,out var page);
        db.DocumentSyncRecords.Add(new(){DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1});await db.SaveChangesAsync();
        if(scenario=="unsupported-html") { db.Pages.Single().Content="<p><img src='/keep.png'></p>";await db.SaveChangesAsync(); }
        var ai=new TranslationOrchestrator();var controller=BuildController(db,ai);
        var sourceController=new WriterApp.Controllers.WebTranslationsController(db,new StubUserIdResolver()){ControllerContext=controller.ControllerContext};
        var captured=Assert.IsType<WebTranslationSource>(Assert.IsType<OkObjectResult>(await sourceController.Source(id,"document",null,default)).Value);
        if(scenario=="stale-fingerprint")captured=captured with { Fingerprint="old" };
        var original=new TranslationStructure(1,id,"document","en",[new(section,[new(page,[new("0.0",scenario=="wrong-text" ? "Invented source" : "Maya checked her phone at 08:05 and sighed.")])])]);
        var request=new AiActionExecuteRequestDto(id,section,page,null,null,null,null,null,new() {
            [TranslationStructures.Parameter]=TranslationStructures.Serialize(original),["target_language"]="en",
            ["web_translation_source"]=System.Text.Json.JsonSerializer.Serialize(captured,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) },"v1");
        var result=await controller.ExecuteAction("translate.document",request,default);Assert.Equal(calls,ai.Calls);
        if(calls==1)Assert.IsType<OkObjectResult>(result.Result);else Assert.True(result.Result is BadRequestObjectResult or ObjectResult { StatusCode:409 });
    }
    [Theory]
    [InlineData("current",1,true)]
    [InlineData("stale",0,false)]
    [InlineData("foreign-owner",0,false)]
    [InlineData("wrong-page",0,false)]
    [InlineData("partial",1,false)]
    [InlineData("late-source",1,false)]
    public async Task TranslationRequiresOwnedCompleteTargetsAndExactSourceEcho(string scenario,int calls,bool success)
    {
        await using var db = BuildDbContext(); SeedDocumentGraph(db,out var id,out var section,out var page);
        db.DocumentSyncRecords.Add(new DocumentSyncRecord { DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1 }); await db.SaveChangesAsync();
        if (scenario == "foreign-owner") { db.Documents.Single().OwnerUserId="other"; await db.SaveChangesAsync(); }
        var source = new TranslationStructure(1,id,"document","en",[new(section,[new(scenario=="wrong-page" ? Guid.NewGuid() : page,[new("0.0","🧭 Åsa 日本語")])])]);
        var ai = new TranslationOrchestrator();
        if (scenario=="late-source") ai.Before=() => { db.DocumentSyncRecords.Single().Version="v2"; db.SaveChanges(); };
        ai.Partial = scenario=="partial";
        var request = new AiActionExecuteRequestDto(id,section,page,null,null,null,null,null,new() {
            [TranslationStructures.Parameter]=TranslationStructures.Serialize(source),["target_language"]="en" },scenario=="stale" ? "old" : "v1");
        var result = await BuildController(db,ai).ExecuteAction("translate.document",request,default);
        Assert.Equal(calls,ai.Calls);
        if (success) {
            var response = Assert.IsType<AiActionExecuteResponseDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Equal("v1",response.SourceDocumentVersion); _ = TranslationStructures.Result(response.ProposedText!,source);
            Assert.Equal(TranslationStructures.Serialize(source),ai.Request!.Context.OriginalText);
            Assert.Contains("Change only runs[].text",ai.Request.Inputs["instruction"].ToString());
        } else Assert.True(result.Result is ConflictObjectResult or NotFoundResult or BadRequestObjectResult
            || result.Result is ObjectResult { StatusCode:502 });
        Assert.Contains("Maya checked",db.Pages.Single().Content);
    }
    private sealed class TranslationOrchestrator : IAiOrchestrator
    {
        public int Calls; public Action? Before; public bool Partial; public AiRequest? Request;
        public IReadOnlyList<IAiAction> Actions => [new TranslateSectionAction(),new TranslateDocumentAction()];
        public IAiAction? GetAction(string id) => Actions.SingleOrDefault(a => a.ActionId==id);
        public bool CanRunAction(string id) => true;
        public AiStreamingCapabilities GetStreamingCapabilities(string id) => new(false,false);
        public Task<AiExecutionResult> ExecuteActionAsync(string id,AiActionInput input,CancellationToken ct) {
            Calls++; Request = GetAction(id)!.BuildRequest(input); Before?.Invoke();
            return Task.FromResult(AiExecutionResult.Success(new(Guid.NewGuid(),input.ActiveSectionId,"Translation",id,"mock",Guid.NewGuid(),DateTime.UtcNow,
                null,[],[],"Translation","document",null,Request.Context.OriginalText,Partial ? "{}" : Request.Context.OriginalText)));
        }
        public AiStreamingSession StreamActionAsync(string id,AiActionInput input,CancellationToken ct) => throw new NotSupportedException();
    }
}
