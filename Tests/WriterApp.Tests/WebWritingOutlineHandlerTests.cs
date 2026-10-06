using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Client.Pages;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DesktopAiWebPersistenceTests
{
    private sealed class OutlineHandler(WritingOutlineSnapshot source) : HttpMessageHandler
    {
        public WritingOutlineSnapshot Source=source;public AiActionExecuteRequestDto? Request;public Func<Task>? After;public int Posts;public Guid Proposal=Guid.NewGuid();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message,CancellationToken ct) {
            if(message.Method==HttpMethod.Get) {
                if(message.RequestUri!.AbsolutePath.Contains("web-source"))return new(HttpStatusCode.OK){Content=JsonContent.Create(new WebAiSource(1,new string('A',64),Source.DocumentId,Source.ProjectId,Source.DocumentVersion,Source.Fingerprint,Source.Sections.Single().Id))};
                return new(HttpStatusCode.OK){Content=JsonContent.Create(Source)};
            }
            Posts++;Request=await message.Content!.ReadFromJsonAsync<AiActionExecuteRequestDto>(ct);
            if(After is not null)await After();
            return new(HttpStatusCode.OK){Content=JsonContent.Create(new AiActionExecuteResponseDto(Proposal,null,"Reviewed prose.",null,DateTimeOffset.UtcNow,Request!.Parameters?.GetValueOrDefault("test_action")?.ToString() ?? message.RequestUri!.AbsolutePath.Split('/')[4],
                SourceDocumentVersion:Request.ExpectedDocumentVersion,SourceOutlineFingerprint:Request.WritingOutline?.Fingerprint,WebSource:Request.WebSource))};
        }
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task WritingRetryRetainsOriginalSourceAndRejectsChangedPlanning(bool changed)
    {
        var id=Guid.NewGuid();var section=Guid.NewGuid();var source=WritingOutline.Create(id,null,"v1",[new(section,0,"Scene")],[]);
        using var handler=new OutlineHandler(source);using var editor=new TranslationEditor{Http=new HttpClient(handler){BaseAddress=new("http://backend/")},AiCommandStatusService=new()};
        typeof(DocumentEditor).GetProperty(nameof(DocumentEditor.DocumentId))!.SetValue(editor,id);TranslationSet(editor,"_activeSection",new SectionDto(section,id,"Scene",null,0,default,default,null,null));
        var method=typeof(DocumentEditor).GetMethod("PostAiActionAsync",TranslationPrivate)!;
        using var first=await (Task<HttpResponseMessage>)method.Invoke(editor,["rewrite.selection",new AiActionExecuteRequestDto(id,section,null,null,null,null,"Writing",null,new()),false,null,CancellationToken.None])!;
        if(changed)handler.Source=WritingOutline.Create(id,null,"v1",[new(section,0,"Later")],[]);
        Task<HttpResponseMessage> Retry()=>(Task<HttpResponseMessage>)method.Invoke(editor,["rewrite.selection",handler.Request!,false,null,CancellationToken.None])!;
        if(changed){await Assert.ThrowsAsync<InvalidOperationException>(Retry);Assert.Equal(1,handler.Posts);}
        else {using var next=await Retry();Assert.Equal(2,handler.Posts);Assert.Equal(source.Fingerprint,handler.Request!.WritingOutline!.Fingerprint);}
    }
    [Theory][InlineData("rewrite.selection")][InlineData("propose.next-paragraph")][InlineData("custom_transform")]
    public async Task ActualWebWritingRequestAdapterPinsSameSharedOutlineForEveryAffectedAction(string key)
    {
        var id=Guid.NewGuid();var section=Guid.NewGuid();var source=WritingOutline.Create(id,null,"v1",[new(section,0,"日本語 <b>Data</b>")],[]);
        using var handler=new OutlineHandler(source);using var editor=new TranslationEditor{Http=new HttpClient(handler){BaseAddress=new("http://backend/")},AiCommandStatusService=new()};
        typeof(DocumentEditor).GetProperty(nameof(DocumentEditor.DocumentId))!.SetValue(editor,id);TranslationSet(editor,"_activeSection",new SectionDto(section,id,"Scene",null,0,default,default,null,null));
        var request=new AiActionExecuteRequestDto(id,section,null,0,7,null,"Writing","old outline",new());
        using var response=await (Task<HttpResponseMessage>)typeof(DocumentEditor).GetMethod("PostAiActionAsync",TranslationPrivate)!.Invoke(editor,[key,request,false,null,CancellationToken.None])!;
        Assert.Equal(source.Fingerprint,handler.Request!.WritingOutline!.Fingerprint);Assert.Equal(source.DocumentVersion,handler.Request.ExpectedDocumentVersion);
        Assert.Null(handler.Request.OutlineText);Assert.Equal("Writing",handler.Request.SurroundingText);
        Assert.Equal(1,handler.Posts);
    }
    [Theory][InlineData("planning")][InlineData("account")][InlineData("backend")][InlineData("section")][InlineData("cancel")]
    public async Task ActualWebWritingAdapterRejectsLateContextWithoutRetainingApproval(string failure)
    {
        var id=Guid.NewGuid();var section=Guid.NewGuid();var source=WritingOutline.Create(id,null,"v1",[new(section,0,"Scene")],[]);
        using var handler=new OutlineHandler(source);using var editor=new TranslationEditor{Http=new HttpClient(handler,false){BaseAddress=new("http://backend/")},AiCommandStatusService=new()};
        typeof(DocumentEditor).GetProperty(nameof(DocumentEditor.DocumentId))!.SetValue(editor,id);TranslationSet(editor,"_activeSection",new SectionDto(section,id,"Scene",null,0,default,default,null,null));
        using var cancel=new CancellationTokenSource();
        handler.After=()=>{if(failure=="planning")handler.Source=WritingOutline.Create(id,null,"v1",[new(section,0,"Changed")],[]);
            if(failure=="account")TranslationSet(editor,"_webTranslationGeneration",1);
            if(failure=="backend")editor.Http=new HttpClient(handler,false){BaseAddress=new("http://other-backend/")};
            if(failure=="section")TranslationSet(editor,"_activeSection",new SectionDto(Guid.NewGuid(),id,"Other",null,0,default,default,null,null));
            if(failure=="cancel")cancel.Cancel();return Task.CompletedTask;};
        var request=new AiActionExecuteRequestDto(id,section,null,null,null,null,"Writing",null,new());
        await Assert.ThrowsAnyAsync<Exception>(()=>(Task<HttpResponseMessage>)typeof(DocumentEditor).GetMethod("PostAiActionAsync",TranslationPrivate)!.Invoke(editor,["rewrite.selection",request,false,null,cancel.Token])!);
        Assert.Empty((System.Collections.IDictionary)TranslationField(editor,"_writingContexts")!);
    }
    [Fact]
    public async Task ActualWebApplyGuardRefusesChangedOutlineBeforeEditorMutation()
    {
        var id=Guid.NewGuid();var section=Guid.NewGuid();var source=WritingOutline.Create(id,null,"v1",[new(section,0,"Scene")],[]);
        using var handler=new OutlineHandler(source);using var editor=new TranslationEditor{Http=new HttpClient(handler){BaseAddress=new("http://backend/")},AiCommandStatusService=new()};
        typeof(DocumentEditor).GetProperty(nameof(DocumentEditor.DocumentId))!.SetValue(editor,id);TranslationSet(editor,"_activeSection",new SectionDto(section,id,"Scene",null,0,default,default,null,null));
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(editor);
        var request=new AiActionExecuteRequestDto(id,section,null,null,null,null,"Writing",null,new());
        using var response=await (Task<HttpResponseMessage>)typeof(DocumentEditor).GetMethod("PostAiActionAsync",TranslationPrivate)!.Invoke(editor,["rewrite.selection",request,false,null,CancellationToken.None])!;
        var pending=Activator.CreateInstance(typeof(DocumentEditor).GetNestedType("PendingAiProposal",BindingFlags.NonPublic)!,[handler.Proposal,"rewrite.selection","Rewrite","Writing","Reviewed prose.",null,null,DateTimeOffset.UtcNow,null])!;
        handler.Source=WritingOutline.Create(id,null,"v1",[new(section,0,"Later saved outline")],[]);
        bool allowed=await renderer.Dispatcher.InvokeAsync(()=>(Task<bool>)typeof(DocumentEditor).GetMethod("ValidateWritingApproval",TranslationPrivate)!.Invoke(editor,[pending])!);
        Assert.False(allowed);Assert.Contains("changed",TranslationField(editor,"_pendingAiProposal")!.ToString());Assert.Equal(1,handler.Posts);
    }
}
