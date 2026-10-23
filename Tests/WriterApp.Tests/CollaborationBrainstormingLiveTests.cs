using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Shared.Brainstorming;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class CollaborationConversionTests
{
    [Theory]
    [InlineData("owner")]
    [InlineData("author")]
    public async Task SharedBrainstormPublishesOnlyValidatedProseAndKeepsPrivateConversationIsolated(string actor)
    {
        await PrepareBrainstormBook(); var provider=DraftProvider();
        using var api=Api(true,configure:s=>BrainstormServices(s,provider,actor));
        using var http=api.CreateClient(); http.DefaultRequestHeaders.Add("X-Test-Owner",actor);
        var (session,story)=await SharedDraft(http,actor);
        string route=$"/api/documents/{_document}/collaboration/brainstorm-draft";
        var mine=(await http.GetFromJsonAsync<BrainstormingSharedDraft>(route))!;
        Assert.True(mine.IsMine); Assert.Equal(session.Drafts.Single().PlainText,mine.PlainText);
        var retained=await _db.CollaborationBrainstormDrafts.AsNoTracking().SingleAsync();
        Assert.Equal(session.Drafts.Single().Id,retained.Id);
        // Replaying a durably accepted operation never publishes a duplicate or dispatches again.
        var ledger=BrainstormingValidation.Json;
        var generation=System.Text.Json.JsonSerializer.Deserialize<BrainstormingGeneration>((await _db.BrainstormingGenerations.AsNoTracking().SingleAsync()).SnapshotJson,ledger)!;
        using var replay=await http.PostAsJsonAsync("/api/brainstorming/conversation/stream",generation.Send,ledger);
        Assert.Equal(HttpStatusCode.OK,replay.StatusCode); Assert.Equal(1,provider.Calls);
        Assert.Equal(1,await _db.CollaborationBrainstormDrafts.CountAsync());
        string other=actor=="owner"?"author":"owner";
        http.DefaultRequestHeaders.Remove("X-Test-Owner");http.DefaultRequestHeaders.Add("X-Test-Owner",other);
        using var response=await http.GetAsync(route); Assert.True(response.Headers.CacheControl!.NoStore);
        var shared=(await response.Content.ReadFromJsonAsync<BrainstormingSharedDraft>())!;
        Assert.False(shared.IsMine);Assert.Equal(mine.PlainText,shared.PlainText);
        string payload=await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Draft a quiet meeting",payload); Assert.DoesNotContain("accountKey",payload); Assert.DoesNotContain("sessionId",payload);
        // Retained publications cannot follow a document GUID into another owner's resource.
        _db.CollaborationBrainstormDrafts.Add(new(){Id=Guid.NewGuid(),DocumentId=_document,BackendKey=session.Scope.BackendKey,
            ResourceOwnerId="different-resource-owner",AuthorId=other,PlainText="Unrelated retained prose",CreatedUnixMilliseconds=DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()});
        await _db.SaveChangesAsync();
        Assert.Equal(mine.Id,(await http.GetFromJsonAsync<BrainstormingSharedDraft>(route))!.Id);
        Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync($"/api/brainstorming/sessions/{session.Scope.SessionId}?documentId={_document}")).StatusCode);
        Assert.Equal(story.Pages.Select(p=>p.Content),(await new WriterApp.Application.AI.BrainstormingStorySourceReader(_db,"owner",Access).ReadAsync(_document)).Pages.Select(p=>p.Content));
        using var different=api.CreateClient();different.BaseAddress=new Uri("https://different-backend.test/");
        different.DefaultRequestHeaders.Add("X-Test-Owner",other);
        Assert.Equal(HttpStatusCode.NoContent,(await different.GetAsync(route)).StatusCode);
    }

    [Fact]
    public async Task SharedBrainstormPublicationRespectsViewerRevocationAndDocumentScope()
    {
        await PrepareBrainstormBook();using var api=Api(true,configure:s=>BrainstormServices(s,DraftProvider(),"author"));
        using var http=api.CreateClient();http.DefaultRequestHeaders.Add("X-Test-Owner","author");
        await SharedDraft(http,"author");
        string route=$"/api/documents/{_document}/collaboration/brainstorm-draft";
        await Sharing.Change("owner",_document,"author",new(1,CollaborationRole.Viewer,false),default);
        Assert.Equal(HttpStatusCode.OK,(await http.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await http.GetAsync($"/api/documents/{Guid.NewGuid()}/collaboration/brainstorm-draft")).StatusCode);
        await Sharing.Change("owner",_document,"author",new(2,CollaborationRole.Viewer,true),default);
        Assert.Equal(HttpStatusCode.Forbidden,(await http.GetAsync(route)).StatusCode);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("failed")]
    [InlineData("revoked")]
    public async Task SharedBrainstormUnvalidatedOrRevokedOutputCannotReplaceThePublishedDraft(string mode)
    {
        await PrepareBrainstormBook();var provider=DraftProvider();
        using var api=Api(true,configure:s=>BrainstormServices(s,provider,"author"));
        using var http=api.CreateClient();http.DefaultRequestHeaders.Add("X-Test-Owner","author");
        var (session,_)=await SharedDraft(http,"author");
        var before=await _db.CollaborationBrainstormDrafts.AsNoTracking().SingleAsync();
        if(mode=="invalid")provider.Wire="PRIVATE invalid provider inspection";
        if(mode=="failed")provider.Mode="fail";
        if(mode=="revoked")provider.BeforeComplete=async()=>{await Sharing.Change("owner",_document,"author",new(1,CollaborationRole.Editor,true),default);};
        var send=new BrainstormingSend(1,session.Scope,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),session.Revision,
            session.Contexts.Single().Id,session.Targets.Single().Id,null,"Draft a different meeting.");
        using var result=await http.PostAsJsonAsync("/api/brainstorming/conversation/stream",send,BrainstormingValidation.Json);
        Assert.Equal(HttpStatusCode.OK,result.StatusCode);
        var terminal=(await result.Content.ReadAsStringAsync()).Split('\n',StringSplitOptions.RemoveEmptyEntries)
            .Select(line=>System.Text.Json.JsonSerializer.Deserialize<BrainstormingConversationEvent>(line,BrainstormingValidation.Json)!).Last();
        Assert.NotEqual(BrainstormingEventKind.Completed,terminal.Kind);Assert.Null(terminal.Output);
        var after=await _db.CollaborationBrainstormDrafts.AsNoTracking().SingleAsync();
        Assert.Equal(before.Id,after.Id);Assert.Equal(before.PlainText,after.PlainText);
    }

    [Fact]
    public async Task SharedProjectPrimaryResolutionIsReadOnlyAndLeavesLegacyWriteFencesIntact()
    {
        await PrepareBrainstormBook();_db.ChangeTracker.Clear();
        var project=await _db.Projects.SingleAsync();Assert.Null(project.PrimaryDocumentId);
        var version=(await _db.DocumentSyncRecords.AsNoTracking().SingleAsync()).Version;
        Assert.Equal(_document,await ManuscriptOwnership.ResolvePrimaryAsync(_db,project,default));
        Assert.Null(project.PrimaryDocumentId);Assert.False(_db.ChangeTracker.HasChanges());
        Assert.Equal(version,(await _db.DocumentSyncRecords.AsNoTracking().SingleAsync()).Version);
        await Assert.ThrowsAnyAsync<Exception>(()=>_db.Pages.ExecuteUpdateAsync(p=>p.SetProperty(x=>x.Content,"stale legacy snapshot")));
    }
}
