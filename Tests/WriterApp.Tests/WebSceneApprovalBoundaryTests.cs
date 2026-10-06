using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Application.AI;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;
public sealed partial class DesktopAiWebPersistenceTests
{
    [Theory][InlineData(false,false)][InlineData(true,false)][InlineData(true,true)]
    public async Task WebSceneApprovalBackendPreservesExactUnselectedRawFieldsAndMirror(bool section,bool inherited) {
        await using var f=new TranslationFixture{Configure=ConfigureCheckedScenes};await f.Start();
        await using(var db=new AppDbContext(f.Options)) {
            (await db.ProjectNodes.SingleAsync()).NodeType=ProjectNodeType.Scene;
            db.SceneCards.Add(new(){SceneNodeId=f.Scene,NarrativePurpose="  Legacy intent  ",NarrativeRole="Revelation",NarrativeIntent="  Original intent  ",Summary="Original",Status="Final",EmotionalBeat="  Beat  ",KeyEvents="  Events  ",OpenQuestions="  Questions  ",PovCharacterId="authored-id",PlaceId="authored-place",TimelineEventId="authored-time",TimeRef="  dawn  ",TagsJson="[ \"Keep\", \"KEEP\" ]",SubplotTagsJson="[ \"Plot\" ]",ReferencesJson=" [ ] "});
            db.SectionSceneCards.Add(new(){SectionId=f.Sections[0],NarrativePurpose="  Legacy intent  ",NarrativeRole="Revelation",NarrativeIntent="  Original intent  ",Summary="Original",Status="Final",EmotionalBeat="  Beat  ",KeyEvents="  Events  ",OpenQuestions="  Questions  ",PovCharacterId="authored-id",PlaceId="authored-place",TimelineEventId="authored-time",TimeRef="  dawn  ",TagsJson="[ \"Keep\", \"KEEP\" ]",SubplotTagsJson="[ \"Plot\" ]",ReferencesJson=" [ ] "});
            await db.SaveChangesAsync();
            if(inherited){db.SectionSceneCards.Remove(await db.SectionSceneCards.SingleAsync());await db.SaveChangesAsync();}
        }
        var source=(await f.Http.GetFromJsonAsync<WebAiSource>($"api/ai/actions/web-source/{f.Document}?sectionId={f.Sections[0]}"+(section?"":$"&sceneId={f.Scene}")))!;
        string url=section?$"api/sections/{f.Sections[0]}/scene-card":$"api/scenes/{f.Scene}/scene-card";
        var current=(await f.Http.GetFromJsonAsync<SceneCardDto>(url))!;
        var proposal=new SectionSceneCardProposalDto(null,null,null,null,Summary:"Approved summary",Status:"Idea",Tags:["Discarded"]);
        var proposalId=Guid.NewGuid();
        await using(var db=new AppDbContext(f.Options)) {
            db.AiActionHistoryEntries.Add(new(){Id=proposalId,OwnerUserId="ordinary-author",DocumentId=f.Document,SectionId=f.Sections[0],ActionKey="scene.suggest",CreatedAt=DateTimeOffset.UtcNow,RequestJson="{}",ResultJson=JsonSerializer.Serialize(new AiActionExecuteResponseDto(proposalId,null,null,null,DateTimeOffset.UtcNow,"scene.suggest",ProposedSceneCard:proposal,WebSource:source),WebAiHistoryContracts.Json)});
            await db.SaveChangesAsync();
        }
        string canonical=WebSceneCardSources.Canonical(current);
        var request=SceneCardApprovals.Request(proposal,[SceneCoachingField.Summary],WebSceneCardSources.Fingerprint(current));
        // Section ID is excluded from both canonical/fingerprint adapters.
        var operation=Guid.NewGuid();var intent=new WebAiHistoryIntent(1,operation,operation,proposalId,1,null,"Applied",source,section?"SectionCard":"SceneCard",canonical,JsonSerializer.Serialize(request,WebAiHistoryContracts.Json));
        using var prepared=await f.Http.PostAsJsonAsync("api/ai/actions/history/web/operations",intent);
        Assert.Equal(HttpStatusCode.OK,prepared.StatusCode);
        object payload=section?new SectionSceneCardUpdateRequest(request.NarrativePurpose,request.EmotionalBeat,request.KeyEvents,request.OpenQuestions,request.PovCharacterId,request.PlaceId,request.TimelineEventId,request.TimeRef,request.Tags,request.References,request.Summary,request.Status,request.SubplotTags,request.NarrativeRole,request.NarrativeIntent,request.ExpectedCardFingerprint,request.ApprovedFields):request;
        using var message=new HttpRequestMessage(HttpMethod.Put,url){Content=JsonContent.Create(payload)};
        message.Headers.Add("X-WriterApp-AI-Source",JsonSerializer.Serialize(source));message.Headers.Add("X-WriterApp-AI-Operation",operation.ToString());
        using var saved=await f.Http.SendAsync(message);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
        await using var verify=new AppDbContext(f.Options);
        var scene=await verify.SceneCards.SingleAsync();var card=await verify.SectionSceneCards.SingleAsync();
        Assert.Equal("Approved summary",section?card.Summary:scene.Summary);
        foreach(var field in Enum.GetValues<SceneCoachingField>().Where(f=>f!=SceneCoachingField.Summary)) {
            string property=SceneCardApprovals.Property(field);
            object? a=typeof(SceneCardRecord).GetProperty(property)!.GetValue(scene);
            object? b=typeof(SectionSceneCardRecord).GetProperty(property)!.GetValue(card);
            Assert.Equal(a,b);
            if(field==SceneCoachingField.Tags)Assert.Equal("[ \"Keep\", \"KEEP\" ]",a);
            if(field==SceneCoachingField.NarrativeIntent)Assert.Equal("  Original intent  ",a);
        }
        Assert.Equal("Final",scene.Status);Assert.Equal("Scene",(await verify.ProjectNodes.SingleAsync()).Title);
        Assert.NotNull((await verify.WebAiHistoryOperations.SingleAsync()).CommittedAt);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task WebSceneApprovalCannotBypassCheckedSourceOrDurableReview(bool section) {
        await using var f=new TranslationFixture{Configure=ConfigureCheckedScenes};await f.Start();
        string url=section?$"api/sections/{f.Sections[0]}/scene-card":$"api/scenes/{f.Scene}/scene-card";
        var payload=new SceneCardUpdateRequest(null,null,null,null,Summary:"Unauthorized partial",ApprovedFields:[SceneCoachingField.Summary]);
        using var response=await f.Http.PutAsJsonAsync(url,payload);Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        await using var db=new AppDbContext(f.Options);Assert.Empty(await db.WebAiHistoryOperations.ToArrayAsync());Assert.Empty(await db.SectionSceneCards.ToArrayAsync());Assert.Empty(await db.SceneCards.ToArrayAsync());
    }
}
