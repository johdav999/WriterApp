using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WriterApp.Shared;
using WriterApp.Application.AI;
using WriterApp.Application.Collaboration;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class CollaborationConversionTests
{
    [Fact]
    public async Task SharedChapterAppendIsScopedIdempotentAndRejectsStaleSourcesAndViewers()
    {
        await Convert();await InviteMember("editor",CollaborationRole.Editor);await InviteMember("viewer",CollaborationRole.Viewer);
        var before=await CollaborationProjectionService.Read(_db,"owner",_document,new(),default,Access);
        var commands=new CollaborationStructureCommands(_db,_codec,Access);
        var source=await new WebAiSourceService(_db,"editor",Access).Capture(_document,null,null,null,default);
        var story=await new BrainstormingStorySourceReader(_db,"editor",Access).ReadAsync(_document);
        Assert.Equal(source.ProjectId,story.Outline.ProjectId); // Stable even before the first chapter.
        var oldScope=new WriterApp.Shared.Brainstorming.BrainstormingScope(1,Guid.NewGuid(),_document,null,WriterApp.Shared.Brainstorming.BrainstormingIdentitySpace.Server,
            WriterApp.Shared.Brainstorming.BrainstormingValidation.Fingerprint("test-backend"),WriterApp.Shared.Brainstorming.BrainstormingValidation.Fingerprint("editor"),null);
        var sessions=new BrainstormingSessions(_db,"editor",oldScope.BackendKey,Access);
        await sessions.WriteAsync(new(1,Guid.NewGuid(),0,WriterApp.Shared.Brainstorming.BrainstormingPersistence.Empty(oldScope) with {Revision=1,State=WriterApp.Shared.Brainstorming.BrainstormingConversationState.Saved}),default);
        var command=new CollaborationStructureCommand(Guid.NewGuid(),source,"create-chapter",Title:"Brainstorm verification chapter");
        Assert.Equal(command.OperationId,await commands.Execute("editor",_document,command,default));
        Assert.Equal(oldScope,(await sessions.ReadAsync(_document,oldScope.SessionId,default))!.Scope);
        Assert.Equal(command.OperationId,await commands.Execute("editor",_document,command,default));
        var chapter=await _db.ProjectNodes.AsNoTracking().SingleAsync(n=>n.Id==command.OperationId);
        Assert.Equal(_document,chapter.DocumentId);Assert.Equal(source.ProjectId,chapter.ProjectId);
        Assert.Equal(WriterApp.Data.Documents.ProjectNodeType.Chapter,chapter.NodeType);Assert.Null(chapter.LinkedSectionId);
        await Assert.ThrowsAnyAsync<Exception>(()=>commands.Execute("editor",_document,command with {OperationId=Guid.NewGuid()},default));
        await Assert.ThrowsAnyAsync<Exception>(()=>commands.Execute("viewer",_document,command with {OperationId=Guid.NewGuid()},default));
        var current=await new WebAiSourceService(_db,"editor",Access).Capture(_document,null,null,null,default);
        await Assert.ThrowsAnyAsync<Exception>(()=>commands.Execute("editor",_document,command with {OperationId=Guid.NewGuid(),Source=current,ParentNodeId=chapter.Id},default));
        Assert.Equal(before.Pages.Select(p=>p.Html),(await CollaborationProjectionService.Read(_db,"owner",_document,new(),default,Access)).Pages.Select(p=>p.Html));
        Assert.Equal(0,await _db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM CollaborationPlanningPermits").SingleAsync());
        Assert.Equal(0,await _db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM CollaborationStructurePermits").SingleAsync());
        await Assert.ThrowsAnyAsync<Exception>(()=>_db.ProjectNodes.Where(n=>n.Id==chapter.Id).ExecuteUpdateAsync(n=>n.SetProperty(x=>x.Title,"Unreviewed overwrite")));
    }
    [Fact]
    public async Task SharedSectionAiApprovalAndScopedHistoryUseCommittedProjection()
    {
        await Convert();await InviteMember("editor",CollaborationRole.Editor);
        using var api=Api(sharing:true);using var http=api.CreateClient();http.DefaultRequestHeaders.Add("X-Test-Owner","editor");
        var section=await _db.Pages.Where(p=>p.Id==_page).Select(p=>p.SectionId).SingleAsync();
        var source=(await http.GetFromJsonAsync<WebTranslationSource>($"/api/documents/{_document}/structured-translations/source?scope=section&sectionId={section}"))!;
        var lease=await new WebAiSourceService(_db,"editor",Access).Capture(_document,section,_page,null,default);
        var pages=source.Sections.Single().Pages.Select(p=> {
            var runs=new List<TranslationRun>();
            void Visit(AngleSharp.Dom.INode node) {
                if(node is AngleSharp.Dom.IText text && text.Data.Length>0)runs.Add(new(runs.Count.ToString(),text.Data));
                foreach(var child in node.ChildNodes)Visit(child);
            }
            Visit(new AngleSharp.Html.Parser.HtmlParser().ParseDocument(p.Content).Body!);
            return new TranslationPage(p.Id,runs);
        }).ToArray();
        var writing=new WritingStructure(1,_document,section,pages);
        var changed=writing with {Pages=pages.Select(p=>p with {Runs=p.Runs.Select(r=>r with {Text=System.Text.RegularExpressions.Regex.Replace(r.Text,@"\S(?=\s*$)","$0 reviewed")}).ToArray()}).ToArray()};
        var original=new TranslationStructure(1,_document,"section","en",[new(section,pages)]);
        var translated=original with {Sections=[new(section,changed.Pages)]};
        var proposal=Guid.NewGuid();var operation=Guid.NewGuid();
        var request=new AiActionExecuteRequestDto(_document,section,_page,null,null,null,null,null,new(){[WritingActions.Parameter]=WritingActions.Serialize(writing)},source.DocumentVersion,WebSource:lease);
        var result=new AiActionExecuteResponseDto(proposal,null,WritingActions.Serialize(changed),null,DateTimeOffset.UtcNow,"expand.section",SourceDocumentVersion:source.DocumentVersion,WebSource:lease);
        _db.AiActionHistoryEntries.Add(new(){Id=proposal,OwnerUserId="editor",DocumentId=_document,SectionId=section,PageId=_page,ActionKey="expand.section",CreatedAt=result.CreatedUtc,RequestJson=JsonSerializer.Serialize(request,WebAiHistoryContracts.Json),ResultJson=JsonSerializer.Serialize(result,WebAiHistoryContracts.Json)});await _db.SaveChangesAsync();
        var intent=new WebAiHistoryIntent(1,operation,operation,proposal,1,null,"Applied",lease,"Aggregate",null,null,operation);
        using var prepare=await http.PostAsJsonAsync("/api/ai/actions/history/web/operations",intent);Assert.Equal(HttpStatusCode.OK,prepare.StatusCode);
        using var approve=await http.PostAsJsonAsync($"/api/documents/{_document}/structured-translations/approve",new WebTranslationApproval(operation,proposal,source,original,translated,"replace","auto",true));Assert.True(approve.IsSuccessStatusCode,await approve.Content.ReadAsStringAsync());
        using var commit=await http.PostAsync($"/api/documents/{_document}/structured-translations/operations/{operation}/commit",null);Assert.True(commit.IsSuccessStatusCode,await commit.Content.ReadAsStringAsync());
        Assert.Contains((await CollaborationProjectionService.Read(_db,"owner",_document,new(),default,Access)).Pages,p=>p.Html.Contains("reviewed"));
        var latest=await new WebAiSourceService(_db,"editor",Access).Capture(_document,section,_page,null,default);
        using var undo=await http.PostAsJsonAsync("/api/ai/actions/history/recovery/replay",new WebRecoveryRequest(Guid.NewGuid(),operation,"Undone",latest));Assert.True(undo.IsSuccessStatusCode,await undo.Content.ReadAsStringAsync());
        Assert.Equal(source.Sections.Single().Pages.Select(p=>p.Content),(await CollaborationProjectionService.Read(_db,"editor",_document,new(),default,Access)).Pages.Where(p=>p.SectionId==section).Select(p=>p.Html));
    }
    [Fact]
    public async Task SharedAggregateReplacementAndRecoveryAreAtomicAndRejectLaterWriting()
    {
        await Convert();await InviteMember("editor",CollaborationRole.Editor);
        var projected=await CollaborationProjectionService.Read(_db,"editor",_document,new(),default,Access);
        var edits=new CollaborationAggregateEdits(_db,_codec,Access);
        async Task<WebAiSource> Source()=>await new WebAiSourceService(_db,"editor",Access).Capture(_document,null,null,null,default);
        WebRecoverySnapshot saved;
        await using(var tx=await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)) {
            saved=await edits.Apply("editor",await Source(),projected.Pages.ToDictionary(p=>p.PageId,p=>"<p>Section approval</p>"),default);
            await tx.CommitAsync();
        }
        Assert.All((await CollaborationProjectionService.Read(_db,"owner",_document,new(),default,Access)).Pages,p=>Assert.Equal("<p>Section approval</p>",p.Html));
        await using(var tx=await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)) {
            await edits.Restore("editor",await Source(),saved,true,default);await tx.CommitAsync();
        }
        Assert.Equal(projected.Pages.Select(p=>p.Html),(await CollaborationProjectionService.Read(_db,"owner",_document,new(),default,Access)).Pages.Select(p=>p.Html));
        await using(var tx=await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)) {
            await edits.Apply("editor",await Source(),new Dictionary<Guid,string>{{_page,"<p>Later writing</p>"}},default);await tx.CommitAsync();
        }
        await using(var tx=await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)) {
            var latest=await Source();
            await Assert.ThrowsAsync<InvalidOperationException>(()=>edits.Restore("editor",latest,saved,false,default));
        }
        Assert.Contains((await CollaborationProjectionService.Read(_db,"owner",_document,new(),default,Access)).Pages,p=>p.Html=="<p>Later writing</p>");
    }
    [Fact]
    public async Task SharedAggregateCodecFailureRollsBackEveryPageAndReceipt()
    {
        await Convert();await InviteMember("editor",CollaborationRole.Editor);
        var before=await CollaborationProjectionService.Read(_db,"editor",_document,new(),default,Access);
        Assert.True(before.Pages.Length>=2);
        var source=await new WebAiSourceService(_db,"editor",Access).Capture(_document,null,null,null,default);
        int receipts=await _db.CollaborationCommands.CountAsync();
        var fault=new FaultCodec(_codec);
        await using(var tx=await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)) {
            await Assert.ThrowsAsync<IOException>(()=>new CollaborationAggregateEdits(_db,fault,Access).Apply("editor",source,
                before.Pages.ToDictionary(p=>p.PageId,p=>"<p>Must not partially commit</p>"),default));
            await tx.RollbackAsync();
        }
        _db.ChangeTracker.Clear();
        var after=await CollaborationProjectionService.Read(_db,"owner",_document,new(),default,Access);
        Assert.Equal(before.Pages.Select(p=>(p.PageId,p.Generation,p.Html)),after.Pages.Select(p=>(p.PageId,p.Generation,p.Html)));
        Assert.Equal(receipts,await _db.CollaborationCommands.CountAsync());
        Assert.Equal(2,fault.Calls);
    }
    [Fact]
    public async Task SharedNotesRejectStaleOtherAccountAndInvalidateAiSource()
    {
        await Convert();var member=await InviteMember("editor",CollaborationRole.Editor);
        using var api=Api(sharing:true);using var owner=api.CreateClient();using var guest=api.CreateClient();
        owner.DefaultRequestHeaders.Add("X-Test-Owner","owner");guest.DefaultRequestHeaders.Add("X-Test-Owner","editor");
        var section=await _db.Pages.Where(p=>p.Id==_page).Select(p=>p.SectionId).SingleAsync();
        string url=$"/api/documents/{_document}/collaboration/tools/sections/{section}/notes";
        var prior=(await owner.GetFromJsonAsync<WriterApp.Application.Documents.SectionNotesDto>(url))!.NotesText;
        var source=await new WebAiSourceService(_db,"owner",Access).Capture(_document,section,_page,null,default);
        using var save=await guest.PutAsJsonAsync(url,new {Before=prior,Notes="Guest notes"});Assert.Equal(HttpStatusCode.OK,save.StatusCode);
        using var stale=await owner.PutAsJsonAsync(url,new {Before=prior,Notes="Owner stale notes"});Assert.Equal(HttpStatusCode.Conflict,stale.StatusCode);
        Assert.Equal("Guest notes",(await owner.GetFromJsonAsync<WriterApp.Application.Documents.SectionNotesDto>(url))!.NotesText);
        await Assert.ThrowsAnyAsync<Exception>(()=>new WebAiSourceService(_db,"owner",Access).Require(source,default));
        await Sharing.Change("owner",_document,"editor",new(member.Revision,CollaborationRole.Viewer,false),default);
        using var denied=await guest.PutAsJsonAsync(url,new {Before="Guest notes",Notes="Viewer write"});Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
    }
    [Fact]
    public async Task CheckedSharedReplacementIsAtomicIdempotentAndRejectsStaleOtherAccount()
    {
        string legacy=(await _db.Pages.AsNoTracking().SingleAsync(p=>p.Id==_page)).Content;
        await Convert();await InviteMember("editor",CollaborationRole.Editor);
        var section=await _db.Pages.Where(p=>p.Id==_page).Select(p=>p.SectionId).SingleAsync();
        var ownerSource=await new WebAiSourceService(_db,"owner",Access).Capture(_document,section,_page,null,default);
        var guestSource=await new WebAiSourceService(_db,"editor",Access).Capture(_document,section,_page,null,default);
        var before=(await CollaborationProjectionService.Read(_db,"owner",_document,new(),default,Access)).Pages.Single(p=>p.PageId==_page);
        var edits=new CollaborationCheckedEdits(_db,_codec,Access);
        var request=new CollaborationCheckedEdit(Guid.NewGuid(),ownerSource,before.Html,"<p>Approved owner replacement</p>");
        var result=await edits.Apply("owner",_document,request,default);
        Assert.Equal(result,await edits.Apply("owner",_document,request,default));
        await Assert.ThrowsAnyAsync<Exception>(()=>edits.Apply("editor",_document,new(Guid.NewGuid(),guestSource,before.Html,"<p>STALE OVERWRITE</p>"),default));
        Assert.True(await _db.CollaborationPages.AnyAsync(p=>p.PageId==_page && p.Generation==before.Generation && p.IsDeleted));
        Assert.Equal(before.Html,(await _db.CollaborationProjections.SingleAsync(p=>p.PageId==_page && p.Generation==before.Generation)).Html);
        var current=(await CollaborationProjectionService.Read(_db,"editor",_document,new(),default,Access)).Pages.Single(p=>p.PageId==_page);
        var fresh=await new WebAiSourceService(_db,"editor",Access).Capture(_document,section,_page,null,default);
        await edits.Apply("editor",_document,new(Guid.NewGuid(),fresh,current.Html,before.Html),default);
        Assert.Equal(before.Html,(await CollaborationProjectionService.Read(_db,"owner",_document,new(),default,Access)).Pages.Single(p=>p.PageId==_page).Html);
        Assert.Equal(legacy,(await _db.Pages.AsNoTracking().SingleAsync(p=>p.Id==_page)).Content);
    }
    [Fact]
    public async Task SharedAiHistoryCommitsWithTextAndUndoRejectsInterveningCollaboratorEdit()
    {
        await Convert();await InviteMember("editor",CollaborationRole.Editor);
        using var api=Api(sharing:true);using var http=api.CreateClient();http.DefaultRequestHeaders.Add("X-Test-Owner","editor");
        var section=await _db.Pages.Where(p=>p.Id==_page).Select(p=>p.SectionId).SingleAsync();
        string sourceUrl=$"/api/ai/actions/web-source/{_document}?sectionId={section}&pageId={_page}";
        var source=(await http.GetFromJsonAsync<WebAiSource>(sourceUrl))!;
        string before=(await CollaborationProjectionService.Read(_db,"editor",_document,new(),default,Access)).Pages.Single(p=>p.PageId==_page).Html;
        Guid proposal=Guid.NewGuid(),operation=Guid.NewGuid();
        _db.AiActionHistoryEntries.Add(new(){Id=proposal,OwnerUserId="editor",DocumentId=_document,SectionId=section,PageId=_page,ActionKey="rewrite.selection",CreatedAt=DateTimeOffset.UtcNow,
            ResultJson=JsonSerializer.Serialize(new AiActionExecuteResponseDto(proposal,"Original","Approved",null,DateTimeOffset.UtcNow,"rewrite.selection",WebSource:source),WebAiHistoryContracts.Json)});
        await _db.SaveChangesAsync();
        var intent=new WebAiHistoryIntent(1,operation,operation,proposal,1,null,"Applied",source,"Page",before,"<p>Approved</p>");
        using var prepared=await http.PostAsJsonAsync("/api/ai/actions/history/web/operations",intent);Assert.Equal(HttpStatusCode.OK,prepared.StatusCode);
        Assert.Null((await _db.WebAiHistoryOperations.AsNoTracking().SingleAsync()).CommittedAt);
        var edit=new CollaborationCheckedEdit(operation,source,before,intent.AfterContent!,operation);
        using var saved=await http.PostAsJsonAsync($"/api/documents/{_document}/collaboration/tools/replace",edit);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
        Assert.True(saved.Headers.Contains("X-WriterApp-AI-Operation"));
        Assert.NotNull((await _db.WebAiHistoryOperations.AsNoTracking().SingleAsync()).CommittedAt);
        using var report=await http.PostAsJsonAsync($"/api/ai/actions/history/web/operations/{operation}/report",intent);Assert.Equal(HttpStatusCode.OK,report.StatusCode);
        var current=(await http.GetFromJsonAsync<WebAiSource>(sourceUrl))!;
        using var move=await http.PostAsJsonAsync("/api/ai/actions/history/web/move",new WebAiHistoryMoveRequest(current,"Undone"));Assert.Equal(HttpStatusCode.OK,move.StatusCode);
        var undo=(await move.Content.ReadFromJsonAsync<WebAiHistoryMove>())!.Intent;
        using var undoPrepared=await http.PostAsJsonAsync("/api/ai/actions/history/web/operations",undo);Assert.Equal(HttpStatusCode.OK,undoPrepared.StatusCode);
        using var undone=await http.PostAsJsonAsync($"/api/documents/{_document}/collaboration/tools/replace",new CollaborationCheckedEdit(undo.OperationId,undo.Source,undo.BeforeContent!,undo.AfterContent!,undo.OperationId));Assert.Equal(HttpStatusCode.OK,undone.StatusCode);
        Assert.Equal(before,(await CollaborationProjectionService.Read(_db,"owner",_document,new(),default,Access)).Pages.Single(p=>p.PageId==_page).Html);
        var ownerSource=await new WebAiSourceService(_db,"owner",Access).Capture(_document,section,_page,null,default);
        await new CollaborationCheckedEdits(_db,_codec,Access).Apply("owner",_document,new(Guid.NewGuid(),ownerSource,before,"<p>Owner's later writing</p>"),default);
        current=(await http.GetFromJsonAsync<WebAiSource>(sourceUrl))!;
        using var redo=await http.PostAsJsonAsync("/api/ai/actions/history/web/move",new WebAiHistoryMoveRequest(current,"Redone"));Assert.Equal(HttpStatusCode.Conflict,redo.StatusCode);
    }
    [Fact]
    public async Task CheckedSharedToolsDenyViewerRevokedAndForeignDocument()
    {
        await Convert();var member=await InviteMember("editor",CollaborationRole.Editor);
        var section=await _db.Pages.Where(p=>p.Id==_page).Select(p=>p.SectionId).SingleAsync();
        var source=await new WebAiSourceService(_db,"editor",Access).Capture(_document,section,_page,null,default);
        var before=(await CollaborationProjectionService.Read(_db,"editor",_document,new(),default,Access)).Pages.Single(p=>p.PageId==_page).Html;
        await Sharing.Change("owner",_document,"editor",new(member.Revision,CollaborationRole.Viewer,false),default);
        var edits=new CollaborationCheckedEdits(_db,_codec,Access);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(()=>edits.Apply("editor",_document,new(Guid.NewGuid(),source,before,"<p>Denied</p>"),default));
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(()=>new CollaborationStructureCommands(_db,_codec,Access).Execute("editor",_document,new(Guid.NewGuid(),source,"create-section"),default));
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(()=>edits.Apply("stranger",_document,new(Guid.NewGuid(),source,before,"<p>Denied</p>"),default));
        var currentMember=await _db.CollaborationMembers.AsNoTracking().SingleAsync(m=>m.DocumentId==_document && m.UserId=="editor");
        await Sharing.Change("owner",_document,"editor",new(currentMember.Revision,CollaborationRole.Viewer,true),default);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(()=>edits.Apply("editor",_document,new(Guid.NewGuid(),source,before,"<p>Revoked write</p>"),default));
        Assert.Equal(before,(await CollaborationProjectionService.Read(_db,"owner",_document,new(),default,Access)).Pages.Single(p=>p.PageId==_page).Html);
    }
    [Fact]
    public async Task SharedStructureUsesCurrentProjectionAndRetainsDeletedWritingAndDatabaseFences()
    {
        await Convert();await InviteMember("editor",CollaborationRole.Editor);
        var service=new CollaborationStructureCommands(_db,_codec,Access);
        async Task<WriterApp.Shared.WebAiSource> Source(string actor)=>await new WebAiSourceService(_db,actor,Access).Capture(_document,null,null,null,default);
        var section=await _db.Pages.Where(p=>p.Id==_page).Select(p=>p.SectionId).SingleAsync();
        var stale=await Source("owner");
        var create=new CollaborationStructureCommand(Guid.NewGuid(),await Source("editor"),"create-section",Title:"Guest chapter");
        var added=await service.Execute("editor",_document,create,default);
        Assert.Equal(added,await service.Execute("editor",_document,create,default));
        await Assert.ThrowsAnyAsync<Exception>(()=>service.Execute("owner",_document,new(Guid.NewGuid(),stale,"rename-section",section,"Stale"),default));
        await service.Execute("owner",_document,new(Guid.NewGuid(),await Source("owner"),"rename-section",added,"Renamed chapter"),default);
        await service.Execute("editor",_document,new(Guid.NewGuid(),await Source("editor"),"reorder-sections",OrderedIds:[added!.Value,section]),default);
        var snapshot=await CollaborationProjectionService.Read(_db,"owner",_document,new(),default,Access);
        Assert.Equal(added,snapshot.Sections.First().SectionId);Assert.Equal("Renamed chapter",snapshot.Sections.First().Title);
        var duplicate=await service.Execute("editor",_document,new(Guid.NewGuid(),await Source("editor"),"duplicate-section",section),default);
        var copy=await CollaborationProjectionService.Read(_db,"owner",_document,new(),default,Access);
        Assert.Equal(snapshot.Pages.Where(p=>p.SectionId==section).Select(p=>p.Html),copy.Pages.Where(p=>p.SectionId==duplicate).Select(p=>p.Html));
        await service.Execute("owner",_document,new(Guid.NewGuid(),await Source("owner"),"delete-section",duplicate),default);
        Assert.DoesNotContain((await CollaborationProjectionService.Read(_db,"editor",_document,new(),default,Access)).Sections,s=>s.SectionId==duplicate);
        Assert.True(await _db.Pages.AnyAsync(p=>p.SectionId==duplicate));
        await Assert.ThrowsAnyAsync<Exception>(()=>_db.Pages.Where(p=>p.Id==_page).ExecuteUpdateAsync(p=>p.SetProperty(x=>x.Content,"UNSAFE")));
        await Assert.ThrowsAnyAsync<Exception>(()=>_db.Sections.Where(s=>s.Id==section).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Title,"UNSAFE")));
    }
}
