using System.Text.Json;
using WriterApp.Application.Documents;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Canon;
using Xunit;

namespace WriterApp.Tests;
public sealed class LocalSceneCoachingTests
{
    internal static DeviceCanonContext Canon(LocalDocument d) => new(d.ServerDocumentId!.Value, d.ServerVersion!,
        Enum.GetValues<CanonKind>().ToDictionary(k => k, k => new DeviceBibleSnapshot(1,d.ServerDocumentId.Value,k,"canon-1",d.ServerVersion,d.ServerVersion!,new string('A',64),DateTimeOffset.UtcNow,
            JsonSerializer.Serialize(new Dictionary<string,object> { ["schemaVersion"]="1", [CanonContent.Collection(k)]=new[]{new Dictionary<string,string>{["id"]=k.ToString().ToLowerInvariant()+"_1",[k==CanonKind.Timeline?"title":"name"]="Label " + k}} }),0,true)));
    internal static AdvancedAiPrepared Prepare(LocalDocument d,SceneCoachingField? field=null,bool refine=false,DeviceCanonContext? canon=null) => LocalSceneCoaching.Prepare(d,d.Sections[0].SectionId,field,"Keep dramatic intent",refine,canon??Canon(d),
        d.Sections[0].Pages.Select(p=>new ConsistencyPageText(p.PageId,DeviceAiRequests.PlainText(p))).ToArray());
    internal static DeviceAiProposal Result(AdvancedAiPrepared p,string json) => new(p.Request,json,"Scene fields","analysis",new(),Guid.NewGuid(),AccountGeneration:1,PreparedAt:DateTimeOffset.UtcNow);
    internal static LocalAiHistory Entry(LocalDocument d,LocalSceneReview review,LocalDocument? after=null,IReadOnlyList<SceneCoachingField>? fields=null) => new(4,Guid.NewGuid(),d.DocumentId,review.Prepared.Request.Key,"SceneCard:"+review.Prepared.Field,
        d.LocalRevision,d.ServerVersion,DateTimeOffset.UtcNow,after is null ? "Reviewed":"Applying",d,review.Raw,NodeId:review.Prepared.NodeId,After:after,SectionId:review.Prepared.SectionId,SceneFields:fields,SceneEntities:review.Entities);
    [Theory]
    [InlineData(SceneCoachingField.Summary,"\"New summary\"")][InlineData(SceneCoachingField.NarrativePurpose,"\"New purpose\"")]
    [InlineData(SceneCoachingField.NarrativeRole,"\"Revelation\"")][InlineData(SceneCoachingField.NarrativeIntent,"\"New intent\"")]
    [InlineData(SceneCoachingField.EmotionalBeat,"\"New beat\"")][InlineData(SceneCoachingField.KeyEvents,"\"New events\"")]
    [InlineData(SceneCoachingField.OpenQuestions,"\"Who opened the door?\"")][InlineData(SceneCoachingField.Status,"\"Final\"")]
    [InlineData(SceneCoachingField.PovCharacterId,"\"character_1\"")][InlineData(SceneCoachingField.PlaceId,"\"place_1\"")]
    [InlineData(SceneCoachingField.TimelineEventId,"\"timeline_1\"")][InlineData(SceneCoachingField.TimeRef,"\"Day 3\"")]
    [InlineData(SceneCoachingField.SubplotTags,"[\"Secrets\"]")][InlineData(SceneCoachingField.Tags,"[\"Mystery\"]")]
    [InlineData(SceneCoachingField.References,"[{\"kind\":\"character\",\"targetId\":\"character_1\",\"note\":\"Present\"}]")]
    public async Task EveryFieldAppliesOnlyApprovedTargetPersistsReopensSyncsAndUndoes(SceneCoachingField field,string value) {
        using var f=new TranslationTestFixture();var d=await f.Create();var p=Prepare(d,field);var canon=Canon(d);
        var review=LocalSceneCoaching.Review(p,Result(p,"{"+JsonSerializer.Serialize(SceneCoaching.Key(field))+":"+value+(field==SceneCoachingField.Summary?"":",\"summary\":\"Ignore this unrelated update\"")+"}"),canon);
        var after=LocalSceneCoaching.Preview(d,review,[field]);var original=d.Project!.Nodes.Single(n=>n.NodeId==p.NodeId).Card??LocalPlanning.EmptyCard;
        var changed=after.Project!.Nodes.Single(n=>n.NodeId==p.NodeId).Card!;
        foreach(var other in Enum.GetValues<SceneCoachingField>().Where(x=>x!=field))Assert.Equal(SceneCoaching.Value(original,other),SceneCoaching.Value(changed,other));
        Assert.Equal(d.Sections,after.Sections);Assert.Equal(field==SceneCoachingField.OpenQuestions?"scene.find-open-questions":"scene.suggest",p.Request.Key);
        var entry=Entry(d,review,after,[field]);await f.History.SaveHistoryAsync(entry);await LocalSceneCoaching.ResumeAsync(f.Repository,f.History,entry);
        var restarted=new LocalDocumentRepository(new FileLocalDocumentStore(f.Root));var saved=(await restarted.LoadAsync(d.DocumentId))!;
        Assert.Equal(changed,saved.Project!.Nodes.Single(n=>n.NodeId==p.NodeId).Card);
        var upload=DeviceSyncMapping.Upload(saved);Assert.Equal(changed,upload.Project!.Nodes.Single(n=>n.Id==(d.Project.Nodes.Single(n=>n.NodeId==p.NodeId).ServerNodeId??p.NodeId)).Card);
        var history=new LocalAiStore(f.Root+"/ai");var actions=new LocalAiHistoryActions(restarted,history);await actions.ChangeAsync(d.DocumentId,entry.Id,false);await actions.ChangeAsync(d.DocumentId,entry.Id,true);
        Assert.Equal(changed,(await restarted.LoadAsync(d.DocumentId))!.Project!.Nodes.Single(n=>n.NodeId==p.NodeId).Card);
    }
    [Theory][InlineData("{\"povCharacterId\":\"Label Character\"}")][InlineData("{\"placeId\":\"foreign_id\"}")]
    [InlineData("{\"timelineEventId\":\"Timeline_1\"}")][InlineData("{\"status\":\"Finished\"}")]
    [InlineData("{\"tags\":[1]}")][InlineData("{\"summary\":{\"text\":\"Bad\"}}")]
    [InlineData("{\"references\":[{\"kind\":\"scene\",\"targetId\":\"foreign\"}]}")][InlineData("{\"references\":[{\"kind\":\"unknown\",\"targetId\":\"character_1\"}]}")]
    [InlineData("{\"references\":[{\"kind\":\"character\"}]}")]
    public async Task MalformedAndForeignFieldsExplainAndPreserveAuthoredValues(string json) {
        using var f=new TranslationTestFixture();var d=await f.Create();var p=Prepare(d);var review=LocalSceneCoaching.Review(p,Result(p,json),Canon(d));
        Assert.All(review.Changes,c=>Assert.NotNull(c.Error));Assert.Throws<InvalidDataException>(()=>LocalSceneCoaching.Preview(d,review,review.Changes.Select(c=>c.Field).ToArray()));
        Assert.Equal(LocalDocumentCodec.Encode(d),LocalDocumentCodec.Encode((await f.Repository.LoadAsync(d.DocumentId))!));
    }
    [Theory][InlineData("{}")][InlineData("{\"summary\":null,\"tags\":[],\"status\":\"\"}")]
    [InlineData("{\"summary\":\"One\",\"Summary\":\"Two\"}")][InlineData("{\"summary\":\"One\",\"summary_\":\"Two\"}")]
    [InlineData("{\"sceneCard\":[]}")][InlineData("{\"summary\":\"One\",\"surprise\":1}")][InlineData("[]")][InlineData("broken")]
    [InlineData("{\"1\":\"Unknown numeric field\"}")]
    public void MissingDuplicateUnknownOrInvalidObjectsCannotBecomeChanges(string json) => Assert.ThrowsAny<Exception>(()=>SceneCoaching.Parse(json,[]));
    [Fact]
    public async Task WholeReviewCanApproveSubsetAndMetadataOnlyResultsNeverDefaultOmittedStatus() {
        using var f=new TranslationTestFixture();var d=await f.Create();var p=Prepare(d);var review=LocalSceneCoaching.Review(p,Result(p,"{\"sceneCard\":{\"summary\":\"New\",\"tags\":[\"A\"],\"pov_character_id\":\"character_1\"},\"explanation\":\"Review\"}"),Canon(d));
        Assert.Equal(3,review.Changes.Count);Assert.DoesNotContain(review.Changes,c=>c.Field==SceneCoachingField.Status);
        var after=LocalSceneCoaching.Preview(d,review,[SceneCoachingField.Tags]);var card=after.Project!.Nodes.Single(n=>n.NodeId==p.NodeId).Card!;Assert.Null(card.Summary);Assert.Null(card.PovCharacterId);Assert.Equal("Draft",card.Status);
        var dto=SceneCoaching.Parse("{\"povCharacterId\":\"character_1\"}",LocalSceneCoaching.Catalogue(d,Canon(d)));Assert.Equal("character_1",dto.Values[SceneCoachingField.PovCharacterId]);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ApprovedRecoveryIsIdempotentBeforeOrAfterCommitAndEvidenceCannotBeReplaced(bool committed) {
        using var f=new TranslationTestFixture();var d=await f.Create();var p=Prepare(d,SceneCoachingField.Status);var review=LocalSceneCoaching.Review(p,Result(p,"{\"status\":\"Final\"}"),Canon(d));var after=LocalSceneCoaching.Preview(d,review,[SceneCoachingField.Status]);var entry=Entry(d,review,after,[SceneCoachingField.Status]);
        await f.History.SaveHistoryAsync(entry);if(committed)await f.Repository.SaveAsync(after);f.Network.SetOnline(false);
        await LocalSceneCoaching.ResumeAsync(f.Repository,f.History,entry);await LocalSceneCoaching.ResumeAsync(f.Repository,f.History,entry);Assert.Equal("Applied",Assert.Single(await f.History.HistoryAsync(d.DocumentId)).Status);
        await Assert.ThrowsAsync<InvalidDataException>(()=>f.History.SaveHistoryAsync(entry with{SceneFields=[SceneCoachingField.Summary]}));
        await Assert.ThrowsAsync<InvalidDataException>(()=>f.History.SaveHistoryAsync(entry with{After=after with{Title="Unapproved"}}));
    }
    [Fact]
    public async Task StaleSourceCanonProjectTargetsAndLaterApprovedFieldEditsAreRejectedButUnrelatedUndoIsPreserved() {
        using var f=new TranslationTestFixture();var d=await f.Create();var p=Prepare(d,SceneCoachingField.Summary);var canon=Canon(d);var review=LocalSceneCoaching.Review(p,Result(p,"{\"summary\":\"New\"}"),canon);
        var ack=d with{LocalRevision=d.LocalRevision+1,UpdatedAtUtc=DateTimeOffset.UtcNow,LastSyncedAtUtc=DateTimeOffset.UtcNow};_ = LocalSceneCoaching.Preview(ack,review,[SceneCoachingField.Summary]);
        Assert.Throws<InvalidOperationException>(()=>LocalSceneCoaching.Preview(d with{Title="Later"},review,[SceneCoachingField.Summary]));
        Assert.Throws<InvalidDataException>(()=>Prepare(d,canon:canon with{DocumentId=Guid.NewGuid()}));
        Assert.Throws<InvalidOperationException>(()=>LocalConsistencyContext.RequireSameCanon(canon,canon with{Snapshots=new Dictionary<CanonKind,DeviceBibleSnapshot>()}));
        var after=LocalSceneCoaching.Preview(d,review,[SceneCoachingField.Summary]);var entry=Entry(d,review,after,[SceneCoachingField.Summary]);await f.History.SaveHistoryAsync(entry);await LocalSceneCoaching.ResumeAsync(f.Repository,f.History,entry);
        var current=(await f.Repository.LoadAsync(d.DocumentId))!;var node=current.Project!.Nodes.Single(n=>n.NodeId==p.NodeId);current=await f.Repository.SaveAsync(LocalPlanning.Scene(current,node.NodeId,node.Card! with{Status="Final"},"Later notes"));
        await new LocalAiHistoryActions(f.Repository,f.History).ChangeAsync(d.DocumentId,entry.Id,false);var undone=(await f.Repository.LoadAsync(d.DocumentId))!;Assert.Equal("Final",undone.Project!.Nodes.Single(n=>n.NodeId==node.NodeId).Card!.Status);Assert.Equal("Later notes",undone.Project.Nodes.Single(n=>n.NodeId==node.NodeId).Notes);
        var applied=entry with{Status="Applied"};Assert.Throws<InvalidOperationException>(()=>LocalAiHistoryActions.Change(LocalPlanning.Scene(after,node.NodeId,node.Card! with{Summary="Author"},""),applied,false));
    }
    [Fact]
    public async Task LegacyJsonPagesUseCompleteSchemaTextWithoutFlatteningSourceAndValidatePlanningLinksInServerNamespace() {
        using var f=new TranslationTestFixture();var d=await f.Create();var scene=d.Project!.Nodes.Single(n=>n.SectionId==d.Sections[0].SectionId);
        Guid cloudScene=Guid.NewGuid();d=d with{Project=d.Project with{Nodes=d.Project.Nodes.Select(n=>n.NodeId==scene.NodeId?n with{ServerNodeId=cloudScene}:n).ToArray()},
            Sections=d.Sections.Select((s,i)=>i==0?s with{Pages=s.Pages.Select(p=>p with{ContentFormat=LocalContentFormat.LegacyJson,Content="{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"Saved legacy text\"}]}]}"}).ToArray()}:s).ToArray()};
        var p=LocalSceneCoaching.Prepare(d,d.Sections[0].SectionId,SceneCoachingField.References,"",false,Canon(d),d.Sections[0].Pages.Select(p=>new ConsistencyPageText(p.PageId,"Saved legacy text")).ToArray());
        Assert.Equal("Saved legacy text\n\nSaved legacy text",p.Request.Request.SurroundingText);
        string json="{\"references\":[{\"kind\":\"scene\",\"targetId\":\""+cloudScene+"\"}]}";var review=LocalSceneCoaching.Review(p,Result(p,json),Canon(d));
        var next=LocalSceneCoaching.Preview(d,review,[SceneCoachingField.References]);Assert.Equal(d.Sections,next.Sections);Assert.Contains(cloudScene.ToString(),next.Project!.Nodes.Single(n=>n.NodeId==scene.NodeId).Card!.ReferencesJson!);
        var localLink=SceneCoaching.Parse(json.Replace(cloudScene.ToString(),scene.NodeId.ToString()),review.Entities);Assert.Contains(SceneCoachingField.References,localLink.Errors.Keys);
    }
    [Fact]
    public async Task TamperedApprovalResultAndCancellationRetainOriginalAndImmutableHistory() {
        using var f=new TranslationTestFixture();var d=await f.Create();var p=Prepare(d,SceneCoachingField.Summary);var review=LocalSceneCoaching.Review(p,Result(p,"{\"summary\":\"New\"}"),Canon(d));var after=LocalSceneCoaching.Preview(d,review,[SceneCoachingField.Summary]);var entry=Entry(d,review,after,[SceneCoachingField.Summary]);
        var node=after.Project!.Nodes.Single(n=>n.NodeId==p.NodeId);
        await Assert.ThrowsAsync<InvalidDataException>(()=>f.History.SaveHistoryAsync(entry with{After=LocalPlanning.Scene(after,node.NodeId,node.Card! with{Summary="Tampered"},node.Notes??"")}));
        using var canceled=new CancellationTokenSource();canceled.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>f.History.SaveHistoryAsync(entry,canceled.Token));Assert.Empty(await f.History.HistoryAsync(d.DocumentId));
        await f.History.SaveHistoryAsync(entry);var later=await f.Repository.SaveAsync(d with{Title="Later source"});await Assert.ThrowsAsync<InvalidOperationException>(()=>LocalSceneCoaching.ResumeAsync(f.Repository,f.History,entry));Assert.Equal(LocalDocumentCodec.Encode(later),LocalDocumentCodec.Encode((await f.Repository.LoadAsync(d.DocumentId))!));
    }
    [Fact]
    public async Task LinkRedoChecksCurrentCatalogueAndRefusesRemovedOrForeignCanon() {
        using var f=new TranslationTestFixture();var d=await f.Create();var p=Prepare(d,SceneCoachingField.PovCharacterId);var canon=Canon(d);var review=LocalSceneCoaching.Review(p,Result(p,"{\"povCharacterId\":\"character_1\"}"),canon);
        var entry=Entry(d,review,LocalSceneCoaching.Preview(d,review,[SceneCoachingField.PovCharacterId]),[SceneCoachingField.PovCharacterId]);
        LocalSceneCoaching.RequireCurrentLinks(d,entry,canon);
        Assert.Throws<InvalidOperationException>(()=>LocalSceneCoaching.RequireCurrentLinks(d,entry,canon with{Snapshots=new Dictionary<CanonKind,DeviceBibleSnapshot>()}));
        Assert.Throws<InvalidDataException>(()=>LocalSceneCoaching.RequireCurrentLinks(d,entry,canon with{DocumentId=Guid.NewGuid()}));
    }
}
