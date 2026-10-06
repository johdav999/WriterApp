using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Shared;

namespace WriterApp.Application.AI;

// Version 2 is scoped to this owned backend graph. IDs are never exported as device-local IDs.
public sealed class WebAggregateRecovery(AppDbContext db)
{
    private static string[] Fields(string kind) => kind switch {
        "Page" => ["Content"], "SectionLanguage" or "DocumentLanguage" => ["LanguageCode"],
        "SceneContent" => ["Exists","ContentJson","LanguageCode"], _ => throw new InvalidDataException("Unsupported aggregate snapshot target.") };
    private static Dictionary<string,string?> Values(object? record,string kind) => Fields(kind).ToDictionary(k=>k,k=>
        k=="Exists" ? record is null?"false":"true" : record is null ? k=="ContentJson"?"":null : (string?)record.GetType().GetProperty(k)!.GetValue(record));
    public async Task<WebRecoverySnapshot> Capture(WebTranslationSource source,CancellationToken ct)
    {
        var targets=new List<WebRecoveryTarget>();
        async Task Add(string kind,Guid id,Guid? section,int? order) {
            var values=Values(await Record(kind,id,ct),kind);targets.Add(new(kind,id,section,order,values,values));
        }
        foreach(var section in source.Sections) {
            foreach(var page in section.Pages)await Add("Page",page.Id,section.Id,page.OrderIndex);
            await Add("SectionLanguage",section.Id,section.Id,section.OrderIndex);
        }
        if(source.Scope=="document")await Add("DocumentLanguage",source.DocumentId,null,null);
        var sectionIds=source.Sections.Select(s=>s.Id).ToArray();
        var nodes=await db.ProjectNodes.AsNoTracking().Where(n=>n.DocumentId==source.DocumentId && n.LinkedSectionId!=null && sectionIds.Contains(n.LinkedSectionId.Value)).Take(1001).ToArrayAsync(ct);
        if(nodes.Length>1000 || nodes.Any(n=>n.ProjectId!=source.ProjectId || n.SyncDeletionId!=null))throw new InvalidDataException("Linked writing mirrors are unavailable or oversized.");
        foreach(var node in nodes)await Add("SceneContent",node.Id,node.LinkedSectionId,null);
        var snapshot=new WebRecoverySnapshot(2,source.DocumentId,source.ProjectId,targets,
            source.Sections.SelectMany(s=>s.Pages.Select(p=>new WebRecoveryPageIdentity(p.Id,s.Id,p.OrderIndex))).ToArray(),
            source.Sections.Select(s=>new WebRecoverySectionIdentity(s.Id,s.OrderIndex)).ToArray());
        _=WebPlanningRecovery.Serialize(snapshot);return snapshot;
    }
    public async Task<WebRecoverySnapshot?> Complete(WebRecoverySnapshot before,CancellationToken ct)
    {
        var targets=new List<WebRecoveryTarget>();
        foreach(var target in before.Targets) {
            var after=Values(await Record(target.Kind,target.Id,ct),target.Kind);
            var keys=target.Before.Keys.Where(k=>target.Before[k]!=after[k]).ToArray();
            // Creation/removal of a mirror owns its whole small record. Never delete later writing.
            if(keys.Contains("Exists"))keys=Fields(target.Kind);
            if(keys.Length>0)targets.Add(target with {Before=keys.ToDictionary(k=>k,k=>target.Before[k]),After=keys.ToDictionary(k=>k,k=>after[k])});
        }
        return targets.Count==0?null:before with {Targets=targets};
    }
    public async Task Restore(WebRecoverySnapshot snapshot,bool undo,CancellationToken ct)
    {
        if(snapshot.Version!=2 || snapshot.Pages is null || snapshot.Sections is null || snapshot.Pages.Count>1000 || snapshot.Sections.Count is <1 or >100
            || snapshot.Targets.Count is <1 or >2101)throw new InvalidDataException("Aggregate snapshot capability is unsupported.");
        var sectionIds=snapshot.Sections.Select(s=>s.Id).ToArray();
        var sections=await db.Sections.AsNoTracking().Where(s=>s.DocumentId==snapshot.DocumentId && sectionIds.Contains(s.Id)).ToArrayAsync(ct);
        if(sections.Length!=snapshot.Sections.Count || snapshot.Sections.Any(s=>!sections.Any(c=>c.Id==s.Id && c.OrderIndex==s.OrderIndex)))
            throw new InvalidOperationException("Affected sections moved or were deleted. No recovery was saved.");
        var pages=await db.Pages.AsNoTracking().Where(p=>p.DocumentId==snapshot.DocumentId && sectionIds.Contains(p.SectionId)).ToArrayAsync(ct);
        if(pages.Length!=snapshot.Pages.Count || snapshot.Pages.Any(p=>!pages.Any(c=>c.Id==p.Id && c.SectionId==p.SectionId && c.OrderIndex==p.OrderIndex)))
            throw new InvalidOperationException("Affected page identity or order changed. No recovery was saved.");
        var records=new List<(WebRecoveryTarget Target,object? Record)>();
        foreach(var target in snapshot.Targets) {
            if(target.Kind=="DocumentLanguage" && target.Id!=snapshot.DocumentId
                || target.Kind=="Page" && !snapshot.Pages.Any(p=>p.Id==target.Id && p.SectionId==target.SectionId && p.OrderIndex==target.OrderIndex)
                || target.Kind=="SectionLanguage" && !snapshot.Sections.Any(s=>s.Id==target.Id && s.OrderIndex==target.OrderIndex)
                || target.Kind=="SceneContent" && !await db.ProjectNodes.AnyAsync(n=>n.Id==target.Id && n.DocumentId==snapshot.DocumentId && n.ProjectId==snapshot.ProjectId && n.LinkedSectionId==target.SectionId && n.SyncDeletionId==null,ct))
                throw new InvalidOperationException("An affected writing target moved or was deleted.");
            var record=await Record(target.Kind,target.Id,ct);
            if(record is null && target.Kind!="SceneContent")throw new InvalidOperationException("An affected writing record was deleted.");
            var actual=Values(record,target.Kind);var expected=undo?target.After:target.Before;
            if(expected.Count==0 || !expected.Keys.Order().SequenceEqual((undo?target.Before:target.After).Keys.Order())
                || expected.Any(f=>!Fields(target.Kind).Contains(f.Key) || actual[f.Key]!=f.Value)
                || target.Kind=="SceneContent" && record is null && expected.GetValueOrDefault("Exists")!="false")
                throw new InvalidOperationException("Affected page content, language or writing mirror changed. Current writing and snapshots were retained.");
            records.Add((target,record));
        }
        // All identities and affected values checked before any write. The controller owns one transaction.
        foreach(var (target,saved) in records) {
            var values=undo?target.Before:target.After;var record=saved;
            if(target.Kind=="SceneContent") {
                if(values.GetValueOrDefault("Exists")=="false") {if(record is not null)db.SceneContents.Remove((SceneContentRecord)record);continue;}
                if(record is null){record=new SceneContentRecord{SceneNodeId=target.Id};db.SceneContents.Add((SceneContentRecord)record);}
            }
            foreach(var value in values.Where(f=>f.Key!="Exists"))record!.GetType().GetProperty(value.Key)!.SetValue(record,value.Value);
            record!.GetType().GetProperty(target.Kind=="SceneContent"?"UpdatedAtUtc":"UpdatedAt")!.SetValue(record,DateTimeOffset.UtcNow);
        }
    }
    private async Task<object?> Record(string kind,Guid id,CancellationToken ct)=>kind switch {
        "Page"=>await db.Pages.SingleOrDefaultAsync(p=>p.Id==id,ct),
        "SectionLanguage"=>await db.Sections.SingleOrDefaultAsync(s=>s.Id==id,ct),
        "DocumentLanguage"=>await db.Documents.SingleOrDefaultAsync(d=>d.Id==id,ct),
        "SceneContent"=>await db.SceneContents.SingleOrDefaultAsync(s=>s.SceneNodeId==id,ct),_=>throw new InvalidDataException("Unknown snapshot target.") };
}
