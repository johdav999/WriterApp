using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;
using WriterApp.Shared;
using Xunit;
namespace WriterApp.Tests;
public sealed partial class CustomTransformActionTests
{
    [Fact]
    public async Task ActualExecutorRetainsMappedCustomResultWithoutFlatReplacementOperations() {
        var document=DocumentFactory.CreateNewDocument();var section=document.Chapters[0].Sections[0].SectionId;
        var mapped=new WritingStructure(1,document.DocumentId,section,[new(Guid.NewGuid(),[new("0.0","Maya waited.")])]);
        var input=new AiActionInput(document,section,new TextRange(0,0),"","Revise",new(){[WritingActions.Parameter]=WritingActions.Serialize(mapped),["context"]="Maya waited.",["scope"]="section",["template"]="Strengthen {context}",["strictTokens"]=true});
        var result=await BuildOrchestrator(new CustomTransformAction()).ExecuteActionAsync("custom_transform",input,default);Assert.True(result.Succeeded);Assert.Empty(result.Proposal!.Operations);Assert.Equal("Section",result.Proposal.TargetScope);_=WritingActions.Result(result.Proposal.ProposedText!,mapped);
    }
}
