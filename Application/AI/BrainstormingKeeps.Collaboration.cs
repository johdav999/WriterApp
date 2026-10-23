using Microsoft.EntityFrameworkCore;
using WriterApp.Data.Documents;
using WriterApp.Shared;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Application.AI;

public sealed partial class BrainstormingKeeps
{
    private async Task ApplyShared(BrainstormingKeepReview r,Guid operation,bool redo,CancellationToken ct)
    {
        var destination=BrainstormingPlacements.Destination(r);
        if(r.Kind!=BrainstormingTargetKind.CreateSection) {
            await RequireUnannotatedPage(r,ct);
            await Writes.Publish(r.Scope.DocumentId,destination.PageId!.Value,operation,r.AfterHtml,r.Before.Pages.Single(p=>p.Id==destination.PageId).Content,ct);
        } else {
            var after=BrainstormingKeepContract.AfterOutline(r);var now=DateTimeOffset.UtcNow;
            if(!redo) {
                if(await db.Sections.AnyAsync(s=>s.Id==r.NewSectionId,ct) || await db.Pages.AnyAsync(p=>p.Id==r.NewPageId,ct) || await db.ProjectNodes.IgnoreQueryFilters().AnyAsync(n=>n.Id==r.NewNodeId,ct))throw new InvalidOperationException("Reviewed section identity is already in use.");
                if(r.Before.Pages.Count>=1000 || after.Sections.Count>1000 || after.Nodes.Count>1000)throw new InvalidOperationException("Shared manuscript structure limit reached.");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Sections(Id,DocumentId,Title,OrderIndex,CreatedAt,UpdatedAt) VALUES({r.NewSectionId},{r.Scope.DocumentId},{r.SectionTitle},{after.Sections.Single(s=>s.Id==r.NewSectionId).Order},{now},{now})",ct);
                // Legacy HTML remains immutable. Shared prose lives only in the new CRDT generation.
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Pages(Id,DocumentId,SectionId,Title,Content,OrderIndex,CreatedAt,UpdatedAt) VALUES({r.NewPageId},{r.Scope.DocumentId},{r.NewSectionId},{"Page 1"},{""},{0},{now},{now})",ct);
            }
            await SetSharedOrder(r,after,ct);
            if(after.Nodes.SingleOrDefault(n=>n.Id==r.NewNodeId) is {} added)
                await Writes.AddNode(r.NewNodeId,r.Scope.DocumentId,r.Scope.ProjectId!.Value,added.ParentId,r.SectionTitle,added.Order,r.NewSectionId,null,ct);
            await Writes.Publish(r.Scope.DocumentId,r.NewPageId,operation,r.AfterHtml,null,ct);
        }
        await Writes.Record(r.Scope.DocumentId,operation,BrainstormingPersistence.Json(r),r.Kind==BrainstormingTargetKind.CreateSection,ct);
    }
    private async Task SetSharedOrder(BrainstormingKeepReview r,WritingOutlineSnapshot outline,CancellationToken ct)
    {
        foreach(var section in outline.Sections)
            await db.Sections.Where(s=>s.Id==section.Id && s.DocumentId==r.Scope.DocumentId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.OrderIndex,section.Order),ct);
        foreach(var node in outline.Nodes.Where(n=>n.Id!=r.NewNodeId))
            await db.ProjectNodes.Where(n=>n.Id==node.Id && n.DocumentId==r.Scope.DocumentId).ExecuteUpdateAsync(n=>n.SetProperty(x=>x.OrderIndex,node.Order),ct);
    }
    private async Task MoveShared(BrainstormingKeepReview r,BrainstormingKeepMove move,CancellationToken ct)
    {
        var current=await new BrainstormingStorySourceReader(db,owner,access).ReadAsync(r.Scope.DocumentId,r.Scope.ProjectId,ct);
        var destination=BrainstormingPlacements.Destination(r);
        if(r.Kind!=BrainstormingTargetKind.CreateSection) {
            await RequireUnannotatedPage(r,ct);
            var before=r.Before.Pages.Single(p=>p.Id==destination.PageId);var page=current.Pages.SingleOrDefault(p=>p.Id==before.Id);
            if(page is null || page.SectionId!=before.SectionId || page.Order!=before.Order || !current.Outline.Sections.Any(s=>s.Id==before.SectionId && s.Order==r.Before.Outline.Sections.Single(s=>s.Id==before.SectionId).Order))throw new InvalidOperationException("Affected writing or order changed. Use original-copy recovery.");
            await Writes.Publish(r.Scope.DocumentId,before.Id,move.OperationId,move.Redo?r.AfterHtml:before.Content,move.Redo?before.Content:r.AfterHtml,ct);
            await Writes.Record(r.Scope.DocumentId,move.OperationId,BrainstormingPersistence.Json(move),false,ct);return;
        }
        var after=BrainstormingKeepContract.AfterOutline(r);var expected=move.Redo?r.Before.Outline:after;
        bool SameOutline()=>current.Outline.Sections.Count==expected.Sections.Count && current.Outline.Nodes.Count==expected.Nodes.Count
            && expected.Sections.All(s=>current.Outline.Sections.Contains(s)) && expected.Nodes.All(n=>current.Outline.Nodes.Contains(n));
        if(!SameOutline())throw new InvalidOperationException("Outline structure changed. Preserve later structure and use original-copy recovery.");
        var added=await db.Sections.AsNoTracking().SingleOrDefaultAsync(s=>s.Id==r.NewSectionId && s.DocumentId==r.Scope.DocumentId,ct);
        var pageRow=await db.Pages.AsNoTracking().SingleOrDefaultAsync(p=>p.Id==r.NewPageId && p.DocumentId==r.Scope.DocumentId,ct);
        var node=await db.ProjectNodes.AsNoTracking().SingleOrDefaultAsync(n=>n.Id==r.NewNodeId && n.DocumentId==r.Scope.DocumentId,ct);
        var expectedNode=after.Nodes.SingleOrDefault(n=>n.Id==r.NewNodeId);
        if(added is null || pageRow is null || added.Title!=r.SectionTitle || added.NarrativePurpose!=null || added.LanguageCode!=null
            || pageRow.SectionId!=r.NewSectionId || pageRow.Title!="Page 1" || pageRow.OrderIndex!=0 || pageRow.Content!=""
            || (move.Redo ? node is not null || current.Pages.Any(p=>p.Id==r.NewPageId) : current.Pages.Count(p=>p.SectionId==r.NewSectionId)!=1 || WriterApp.Application.Collaboration.CollaborationBrainstormingWrites.Canonical(current.Pages.Single(p=>p.Id==r.NewPageId).Content)!=WriterApp.Application.Collaboration.CollaborationBrainstormingWrites.Canonical(r.AfterHtml)
                || (expectedNode is null ? node is not null : node is null || node.MetadataJson!=null || node.LinkedSectionId!=r.NewSectionId || node.NodeType!=ProjectNodeType.Scene))
            || await db.SectionNotes.AnyAsync(n=>n.SectionId==r.NewSectionId,ct) || await db.PageNotes.AnyAsync(n=>n.PageId==r.NewPageId,ct) || await db.PageAnnotations.AnyAsync(n=>n.PageId==r.NewPageId,ct)
            || await db.SceneCards.AnyAsync(n=>n.SceneNodeId==r.NewNodeId,ct) || await db.SceneNotes.AnyAsync(n=>n.SceneNodeId==r.NewNodeId,ct) || await db.SceneContents.AnyAsync(n=>n.SceneNodeId==r.NewNodeId,ct) || await db.SceneAnnotations.AnyAsync(n=>n.SceneNodeId==r.NewNodeId,ct)
            || await db.Pages.CountAsync(p=>p.SectionId==r.NewSectionId,ct)!=1)
            throw new InvalidOperationException("Added section has later writing or planning. Preserve it and use original-copy recovery.");
        if(move.Redo)await ApplyShared(r,move.OperationId,true,ct);
        else {
            await Writes.Retire(r.Scope.DocumentId,r.NewPageId,ct);
            await SetSharedOrder(r,r.Before.Outline,ct);
            await db.ProjectNodes.Where(n=>n.Id==r.NewNodeId && n.DocumentId==r.Scope.DocumentId).ExecuteDeleteAsync(ct);
            await Writes.Record(r.Scope.DocumentId,move.OperationId,BrainstormingPersistence.Json(move),true,ct);
        }
    }
}
