using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Data;
using WriterApp.Shared;

namespace WriterApp.Application.Collaboration;

public static class CollaborationWritingOutline
{
    public static async Task<WritingOutlineSnapshot> Read(AppDbContext db,CollaborationAccessService access,string actor,Guid document,CancellationToken ct)
    {
        await using var tx=db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct) : null;
        var source=await new WebAiSourceService(db,actor,access).Capture(document,null,null,null,ct);
        var snapshot=await CollaborationProjectionService.Read(db,actor,document,new(),ct,access,CollaborationOperation.PrepareAi);
        var nodes=await db.ProjectNodes.AsNoTracking().Where(n=>n.DocumentId==document && n.ProjectId==source.ProjectId && n.SyncDeletionId==null)
            .OrderBy(n=>n.Id).Take(WritingOutline.MaxEntities+1).ToArrayAsync(ct);
        if(nodes.Length>WritingOutline.MaxEntities)throw new InvalidDataException("Shared outline is oversized.");
        var activeSections=snapshot.Sections.Select(s=>s.SectionId).ToHashSet();
        var excluded=nodes.Where(n=>n.LinkedSectionId is { } section && !activeSections.Contains(section)).Select(n=>n.Id).ToHashSet();
        while(nodes.Any(n=>!excluded.Contains(n.Id) && n.ParentId is { } parent && excluded.Contains(parent)))
            foreach(var node in nodes.Where(n=>n.ParentId is { } parent && excluded.Contains(parent)))excluded.Add(node.Id);
        var result=WritingOutline.Create(document,source.ProjectId,source.DocumentVersion,
            snapshot.Sections.Select(s=>new WritingOutlineSection(s.SectionId,s.OrderIndex,s.Title)),
            nodes.Where(n=>!excluded.Contains(n.Id)).Select(n=>new WritingOutlineNode(n.Id,n.ParentId,n.OrderIndex,n.NodeType.ToString().ToLowerInvariant(),n.Title,n.LinkedSectionId)));
        if(tx is not null)await tx.CommitAsync(ct);
        return result;
    }
}
