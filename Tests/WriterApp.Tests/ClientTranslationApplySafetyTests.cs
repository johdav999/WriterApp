using System.Net;
using System.Reflection;
using WriterApp.Application.Documents;
using WriterApp.Client.Pages;
using Xunit;

namespace WriterApp.Tests;

public sealed class ClientTranslationApplySafetyTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [Theory]
    [InlineData("translate.section", "replace", false)]
    [InlineData("translate.section", "duplicate-section", false)]
    [InlineData("translate.document", "replace", false)]
    [InlineData("translate.document", "duplicate-document", false)]
    [InlineData("translate.section", "replace", true)]
    [InlineData("translate.section", "duplicate-section", true)]
    [InlineData("translate.document", "replace", true)]
    [InlineData("translate.document", "duplicate-document", true)]
    public async Task LegacyBroaderTranslationCannotWriteOrReportAppliedAndRetainsTheReview(string action, string mode, bool direct)
    {
        using var handler=new RecordingHandler();using var http=new HttpClient(handler){BaseAddress=new Uri("https://test.invalid/")};
        var component=new DocumentEditor{Http=http};
        var type=typeof(DocumentEditor);var pendingType=type.GetNestedType("PendingAiProposal",BindingFlags.NonPublic)!;
        var id=Guid.NewGuid();const string source="<h2>Åsa 日本語 🧭</h2><p><strong>Keep rich writing</strong></p>";
        var page=new PageDto(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"Page",source,0,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow);
        var pending=Activator.CreateInstance(pendingType,[id,action,"Translation",source,"Translated text",null,null,DateTimeOffset.UtcNow,null])!;
        type.GetField("_pendingAiProposal",Private)!.SetValue(component,pending);
        type.GetField("_activePage",Private)!.SetValue(component,page);
        type.GetField("_translationApplyMode",Private)!.SetValue(component,mode);
        // Exercise both the button handler and its Apply helper; no mounted editor/provider is needed to reject unsafe proposals.
        var method=type.GetMethod(direct?"ApplyTranslationProposalAsync":"OnApplyPendingAiProposal",Private)!;
        await (Task)method.Invoke(component,direct?[pending]:null)!;
        Assert.Equal(0,handler.Requests);Assert.Equal(page,type.GetField("_activePage",Private)!.GetValue(component));
        var retained=type.GetField("_pendingAiProposal",Private)!.GetValue(component)!;
        Assert.Equal(id,pendingType.GetProperty("ProposalId")!.GetValue(retained));
        Assert.Equal(source,pendingType.GetProperty("OriginalText")!.GetValue(retained));
        Assert.Equal("Translated text",pendingType.GetProperty("ProposedText")!.GetValue(retained));
        Assert.Contains("cannot preserve all pages and formatting",(string)pendingType.GetProperty("ErrorMessage")!.GetValue(retained)!);
        Assert.False((bool)type.GetField("_hasAiUndoHistory",Private)!.GetValue(component)!);
    }
    [Fact]
    public void SelectionTranslationAndOtherActionsKeepTheirExistingApplyContract()
    {
        var method=typeof(DocumentEditor).GetMethod("GetTranslationApplyRestriction",BindingFlags.Static|BindingFlags.NonPublic)!;
        Assert.Null(method.Invoke(null,["translate.selection"]));Assert.Null(method.Invoke(null,["rewrite.selection"]));
    }
    private sealed class RecordingHandler:HttpMessageHandler {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Requests++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));}
    }
}
