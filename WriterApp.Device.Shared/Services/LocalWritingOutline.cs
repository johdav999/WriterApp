using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;

namespace WriterApp.Device.Shared.Services;

public static class LocalWritingOutline
{
    public static DeviceAiPrepared Bind(DeviceAiPrepared prepared,LocalDocument document) => !WritingOutline.Consumes(prepared.Key) ? prepared
        : prepared with{Request=prepared.Request with{OutlineText=null,WritingOutline=Capture(document)}};
    public static WritingOutlineSnapshot Capture(LocalDocument document)
    {
        if(document.ServerDocumentId is not { } id || document.ServerVersion is not { } version
            || document.SyncState!=LocalSyncState.Synced || document.DeletedAtUtc is not null)
            throw new InvalidOperationException("Save and synchronize this document before capturing its writing outline.");
        var sections=document.Sections.Select(s=>new WritingOutlineSection(s.ServerSectionId
            ?? throw new InvalidOperationException("Synchronize every outline section first."),s.OrderIndex,s.Title));
        var active=document.Project?.Nodes.Where(n=>n.DeletionId is null).ToArray() ?? [];
        var ids=active.ToDictionary(n=>n.NodeId,n=>n.ServerNodeId ?? throw new InvalidOperationException("Synchronize every outline node first."));
        var sectionIds=document.Sections.ToDictionary(s=>s.SectionId,s=>s.ServerSectionId!.Value);
        if(document.Project is { } project && (project.ManuscriptId!=document.DocumentId || project.ServerProjectId is null))
            throw new InvalidOperationException("This saved outline belongs to another manuscript or an unsynchronized project.");
        var nodes=active.Select(n=>new WritingOutlineNode(ids[n.NodeId],n.ParentId is { } parent ? ids.GetValueOrDefault(parent) : null,
            n.OrderIndex,n.NodeType.ToLowerInvariant(),n.Title,n.SectionId is { } section ? sectionIds.GetValueOrDefault(section) : null));
        return WritingOutline.Create(id,document.Project?.ServerProjectId ?? document.ServerProjectId,version,sections,nodes);
    }
    public static void RequireCurrent(LocalDocument document, WritingOutlineSnapshot snapshot)
    {
        if(Capture(document).Fingerprint!=snapshot.Fingerprint)throw new InvalidOperationException("Saved outline changed. Generate and review again before applying.");
    }
}
