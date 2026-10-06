using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;
#pragma warning disable BL0006 // Dispatch actual compiled component event bindings.
public sealed class SceneApprovalReviewTests
{
    private static readonly SceneFieldChange[] Changes=[new(SceneCoachingField.Summary,"Authored summary","<script>inert 日本語 🧭</script>"),
        new(SceneCoachingField.KeyEvents,"Authored events","Proposed events"),new(SceneCoachingField.PlaceId,"Authored place","Foreign","Unresolved place")];
    private sealed class ReviewRenderer(IServiceProvider services,ILoggerFactory logs) : Renderer(services,logs) {
        private int _root;private bool _attached;private readonly SceneCoachingReview _component=new();
        public IReadOnlyList<SceneCoachingField> Approved=[];
        public override Dispatcher Dispatcher {get;}=Dispatcher.CreateDefault();
        protected override void HandleException(Exception e)=>throw new InvalidOperationException("Review failed",e);
        protected override Task UpdateDisplayAsync(in RenderBatch batch)=>Task.CompletedTask;
        public Task Show(bool busy=false)=>Dispatcher.InvokeAsync(()=> {
            if(!_attached){_root=AssignRootComponentId(_component);_attached=true;}
            return RenderRootComponentAsync(_root,ParameterView.FromDictionary(new Dictionary<string,object?> {
                [nameof(SceneCoachingReview.Changes)]=Changes,[nameof(SceneCoachingReview.Approved)]=Approved,[nameof(SceneCoachingReview.Busy)]=busy,
                [nameof(SceneCoachingReview.ApprovedChanged)]=EventCallback.Factory.Create<IReadOnlyList<SceneCoachingField>>(new object(),fields=>Approved=fields)
            }));
        });
        public Task Dispatch(string attribute,int index,EventArgs args)=>Dispatcher.InvokeAsync(()=> {
            var frames=GetCurrentRenderTreeFrames(_root);
            var handler=frames.Array.Take(frames.Count).Where(f=>f.FrameType==RenderTreeFrameType.Attribute && f.AttributeName==attribute).ElementAt(index).AttributeEventHandlerId;
            return DispatchEventAsync(handler,null,args);
        });
    }
    [Fact]
    public async Task SceneApprovalRenderedCheckboxAllAndClearDispatchTheSharedSelectionContract() {
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new ReviewRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Show();await renderer.Dispatch("onchange",1,new ChangeEventArgs{Value=true});Assert.Equal(new[]{SceneCoachingField.KeyEvents},renderer.Approved);
        await renderer.Show();await renderer.Dispatch("onclick",0,new MouseEventArgs());Assert.Equal(new[]{SceneCoachingField.Summary,SceneCoachingField.KeyEvents},renderer.Approved);
        await renderer.Show();await renderer.Dispatch("onclick",1,new MouseEventArgs());Assert.Empty(renderer.Approved);
        await renderer.Show();await renderer.Dispatch("onchange",0,new ChangeEventArgs{Value=true});Assert.Equal(new[]{SceneCoachingField.Summary},renderer.Approved);
        await renderer.Show();await renderer.Dispatch("onchange",0,new ChangeEventArgs{Value=false});Assert.Empty(renderer.Approved);
    }
    [Theory][InlineData("editor")][InlineData("inspector")][InlineData("desktop")]
    public async Task SceneApprovalSharedReviewRendersInertChangedFieldsAndSelectionStates(string host) {
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        foreach(var state in new[]{"empty","partial","all","busy"}) {
            SceneCoachingField[] approved=state is "all" or "busy"?[SceneCoachingField.Summary,SceneCoachingField.KeyEvents]:state=="partial"?[SceneCoachingField.KeyEvents]:[];
            var html=await renderer.Dispatcher.InvokeAsync(async()=> (await renderer.RenderComponentAsync<SceneCoachingReview>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                [nameof(SceneCoachingReview.Changes)]=Changes,[nameof(SceneCoachingReview.Approved)]=approved,[nameof(SceneCoachingReview.Busy)]=state=="busy"
            }))).ToHtmlString());
            Assert.Contains("Approve all changed fields",html);Assert.Contains("Clear selection",html);Assert.Contains("Unresolved place",html);
            Assert.DoesNotContain("<script>",html);Assert.Contains("&lt;script&gt;",html);
            var dom=new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);Assert.Equal(2,dom.QuerySelectorAll("input[type=checkbox]").Length);
            Assert.Equal(approved.Length,dom.QuerySelectorAll("input[checked]").Length);
            if(state=="busy")Assert.Equal(2,dom.QuerySelectorAll("input[disabled]").Length);
            var root=Environment.GetEnvironmentVariable("WRITERAPP_P03_EVIDENCE");if(root is not null){Directory.CreateDirectory(root);await File.WriteAllTextAsync(Path.Combine(root,host+"-"+state+".html"),html);}
        }
    }
    [Fact]
    public void SceneApprovalUnchangedOmittedAndInvalidFieldsCannotBeSelected() {
        var before=new SectionSceneCardProposalDto(null,null,null,null,Summary:"Same",Status:"Final");
        var proposed=before with {EmotionalBeat="New",KeyEvents="",Status="Unsupported"};
        var changes=SceneCardApprovals.Changes(before,proposed);Assert.Equal(2,changes.Count);
        Assert.Throws<InvalidDataException>(()=>SceneCardApprovals.Require([SceneCoachingField.Status],changes));
        Assert.Throws<InvalidDataException>(()=>SceneCardApprovals.Require([SceneCoachingField.Summary],changes));
        Assert.Throws<InvalidDataException>(()=>SceneCardApprovals.Require([],changes));
        SceneCardApprovals.Require([SceneCoachingField.EmotionalBeat],changes);
    }
    [Theory][InlineData("Revelation",SceneCoachingField.NarrativeRole)][InlineData("Raise the tension",SceneCoachingField.NarrativeIntent)]
    public void SceneApprovalLegacyPurposeUsesTheEditableRoleOrIntentField(string purpose,SceneCoachingField field) {
        var before=new SectionSceneCardProposalDto(null,null,null,null,Status:null);
        var proposed=before with {NarrativePurpose=purpose};
        var change=Assert.Single(SceneCardApprovals.Changes(before,proposed));Assert.Equal(field,change.Field);
        var request=SceneCardApprovals.Request(proposed,[field],new string('A',64));
        Assert.Null(request.NarrativePurpose);Assert.Equal(purpose,field==SceneCoachingField.NarrativeRole?request.NarrativeRole:request.NarrativeIntent);
    }
}
