using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents;

// Shared by ordinary page saving and checked aggregate writing transactions.
public static class ManuscriptSceneMirror
{
    public static async Task UpdateAsync(AppDbContext db,Guid sectionId,CancellationToken ct)
    {
        var nodes=await db.ProjectNodes.Where(n=>n.NodeType==ProjectNodeType.Scene && n.LinkedSectionId==sectionId && n.SyncDeletionId==null).Select(n=>n.Id).ToArrayAsync(ct);
        if(nodes.Length==0)return;
        var section=await db.Sections.AsNoTracking().SingleAsync(s=>s.Id==sectionId,ct);
        var content=string.Join("\n\n",await db.Pages.AsNoTracking().Where(p=>p.SectionId==sectionId).OrderBy(p=>p.OrderIndex).ThenBy(p=>p.Id).Select(p=>p.Content).ToArrayAsync(ct));
        var existing=await db.SceneContents.Where(s=>nodes.Contains(s.SceneNodeId)).ToDictionaryAsync(s=>s.SceneNodeId,ct);
        foreach(var id in nodes){if(!existing.TryGetValue(id,out var scene)){scene=new(){SceneNodeId=id};db.SceneContents.Add(scene);}scene.ContentJson=content;scene.LanguageCode=section.LanguageCode;scene.UpdatedAtUtc=DateTimeOffset.UtcNow;}
        await db.SaveChangesAsync(ct);
    }
}
