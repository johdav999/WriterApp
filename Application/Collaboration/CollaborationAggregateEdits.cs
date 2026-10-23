using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Data;
using WriterApp.Shared;

namespace WriterApp.Application.Collaboration;

// Caller owns the serializable approval/receipt transaction. Every page generation
// and its retained before/after snapshot commits together, including recovery.
public sealed class CollaborationAggregateEdits(AppDbContext db,ICollaborationCodec codec,CollaborationAccessService access)
{
    public async Task<WebRecoverySnapshot> Apply(string actor,WebAiSource source,IReadOnlyDictionary<Guid,string> replacements,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction is null || replacements.Count is <1 or >1000 || replacements.Values.Sum(s=>(long)s.Length)>2_000_000)
            throw new InvalidOperationException("Checked section changes require one bounded transaction.");
        await CollaborationMigrationService.Lock(db,source.DocumentId,ct);
        await new WebAiSourceService(db,actor,access).Require(source,ct);
        var before=await CollaborationProjectionService.Read(db,actor,source.DocumentId,new(),ct,access,CollaborationOperation.WriteText);
        if(replacements.Keys.Any(id=>!before.Pages.Any(p=>p.PageId==id)))throw new InvalidDataException("A reviewed page was removed.");
        var targets=new List<WebRecoveryTarget>();
        foreach(var page in before.Pages.Where(p=>replacements.ContainsKey(p.PageId))) {
            var current=await new WebAiSourceService(db,actor,access).Capture(source.DocumentId,page.SectionId,page.PageId,null,ct);
            var result=await new CollaborationCheckedEdits(db,codec,access).Apply(actor,source.DocumentId,new(Guid.NewGuid(),current,page.Html,replacements[page.PageId]),ct);
            targets.Add(new("Page",page.PageId,page.SectionId,page.OrderIndex,new Dictionary<string,string?>{{"Content",page.Html}},new Dictionary<string,string?>{{"Content",result.Html}}));
        }
        var sections=before.Sections.Where(s=>targets.Any(t=>t.SectionId==s.SectionId)).ToArray();
        var snapshot=new WebRecoverySnapshot(3,source.DocumentId,source.ProjectId,targets,
            before.Pages.Where(p=>sections.Any(s=>s.SectionId==p.SectionId)).Select(p=>new WebRecoveryPageIdentity(p.PageId,p.SectionId,p.OrderIndex)).ToArray(),
            sections.Select(s=>new WebRecoverySectionIdentity(s.SectionId,s.OrderIndex)).ToArray());
        _=WebPlanningRecovery.Serialize(snapshot);return snapshot;
    }
    public async Task Restore(string actor,WebAiSource source,WebRecoverySnapshot snapshot,bool undo,CancellationToken ct)
    {
        if(snapshot.Version!=3 || snapshot.DocumentId!=source.DocumentId || snapshot.ProjectId!=source.ProjectId || snapshot.Pages is null || snapshot.Sections is null || snapshot.Targets.Count is <1 or >1000)
            throw new InvalidDataException("Unsupported shared recovery snapshot.");
        await CollaborationMigrationService.Lock(db,source.DocumentId,ct);
        var current=await CollaborationProjectionService.Read(db,actor,source.DocumentId,new(),ct,access,CollaborationOperation.WriteText);
        if(snapshot.Sections.Any(s=>!current.Sections.Any(c=>c.SectionId==s.Id && c.OrderIndex==s.OrderIndex))
            || current.Pages.Count(p=>snapshot.Sections.Any(s=>s.Id==p.SectionId))!=snapshot.Pages.Count
            || snapshot.Pages.Any(p=>!current.Pages.Any(c=>c.PageId==p.Id && c.SectionId==p.SectionId && c.OrderIndex==p.OrderIndex)))
            throw new InvalidOperationException("Reviewed sections or pages moved. Current writing is retained.");
        var replacements=new Dictionary<Guid,string>();
        foreach(var target in snapshot.Targets) {
            var page=current.Pages.SingleOrDefault(p=>p.PageId==target.Id && p.SectionId==target.SectionId && p.OrderIndex==target.OrderIndex);
            var expected=undo?target.After:target.Before;var replacement=undo?target.Before:target.After;
            if(target.Kind!="Page" || expected.Count!=1 || replacement.Count!=1 || page is null || page.Html!=expected.GetValueOrDefault("Content") || replacement.GetValueOrDefault("Content") is not {} html)
                throw new InvalidOperationException("A collaborator changed an affected page. No recovery was saved.");
            replacements.Add(target.Id,html);
        }
        await Apply(actor,source,replacements,ct);
    }
}
