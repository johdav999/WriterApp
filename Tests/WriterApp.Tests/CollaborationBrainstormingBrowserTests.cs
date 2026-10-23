using Microsoft.EntityFrameworkCore;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class CollaborationSessionTests
{
    private bool _brainstormUi;
    [CollaborationBrowserFact]
    public async Task NormalProsaBrainstormDraftKeepPlanningUndoRedoAndReload()
    {
        _brainstormUi=true;_localTestUi=true;_clock.RealTime=true;_settings.CurrentValue.CommitsEnabled=true;_sharingOptions.CurrentValue.Enabled=true;_interactions.Enabled=true;
        await using(var db=Open())
        {
            // This class owns a unique disposable database. Use the actual migration
            // history and triggers so capability checks and reviewed saves are real.
            await db.Database.EnsureDeletedAsync();await db.Database.MigrateAsync();
            foreach(var actor in new[]{"owner","editor"})db.ExternalIdentityLinks.Add(new(){UserId=actor,Issuer="https://fixture.test/",ObjectIdentifier=actor,Subject=actor,EmailAtLinkTime=actor+"@fixture.test"});
            var document=Guid.Parse("1396210e-fbac-4714-bb20-37560d6b40bf");var section=Guid.NewGuid();var chapter=Guid.NewGuid();var project=Guid.NewGuid();
            db.Projects.Add(new(){Id=project,OwnerUserId="owner",SyncEnabled=true});
            db.Documents.Add(new(){Id=document,ProjectId=project,OwnerUserId="owner",DocumentKind=DocumentKind.Manuscript,Title="Brainstorm acceptance manuscript"});
            db.Sections.Add(new(){Id=section,DocumentId=document,Title="First scene"});
            db.Pages.Add(new(){Id=Guid.NewGuid(),DocumentId=document,SectionId=section,Title="First page",Content="<p>Existing writing retained.</p>"});
            db.ProjectNodes.AddRange(new(){Id=chapter,ProjectId=project,DocumentId=document,NodeType=ProjectNodeType.Chapter,Title="First chapter"},new(){Id=Guid.NewGuid(),ProjectId=project,DocumentId=document,ParentId=chapter,NodeType=ProjectNodeType.Scene,Title="First scene",LinkedSectionId=section});
            await db.SaveChangesAsync();
        }
        await RunProbe("brainstorm-ui-browser-probe.mjs",Request);
    }
}
