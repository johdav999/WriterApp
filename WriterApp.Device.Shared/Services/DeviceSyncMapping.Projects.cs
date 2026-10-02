using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Sync;

namespace WriterApp.Device.Shared.Services;

internal static partial class DeviceSyncMapping
{
    private static SyncProject? UploadProject(LocalDocument doc)
    {
        if (doc.Project is not { } p) return null;
        var nodes = p.Nodes.ToDictionary(n => n.NodeId);
        var sections = doc.Sections.ToDictionary(s => s.SectionId);
        return new(p.ServerProjectId ?? p.ProjectId, p.Title, p.Nodes.Select(n => new SyncProjectNode(n.ServerNodeId ?? n.NodeId,
            n.ParentId is { } parent ? nodes[parent].ServerNodeId ?? parent : null, n.NodeType, n.Title, n.OrderIndex,
            n.SectionId is { } section ? sections[section].ServerSectionId ?? section : null, n.DeletionId, n.MetadataJson, n.Notes, n.Card, n.Annotations.Select(a => a.Value with { Id = a.ServerId ?? a.LocalId }).OrderBy(a => a.Id).ToArray()))
            .OrderBy(n => n.Id).ToArray(), p.Subtitle, p.AuthorName, p.Language, p.Genre, p.DefaultExportSettingsJson, p.CoverImageUrl, Version: p.Version >= 3 ? 3 : 2, Synopsis: p.Synopsis,
            PrimaryDocumentId: p.PrimaryDocumentId == doc.DocumentId ? doc.ServerDocumentId : p.ServerPrimaryDocumentId,
            MetadataRevision: p.ServerMetadataRevision ?? 0);
    }
    private static LocalProject? DownloadProject(SyncDocument document, Guid localId, LocalDocument? previous)
    {
        if (document.Project is not { } p) return null;
        if (p.Version is not (1 or 2 or 3) || p.Id != document.ProjectId || p.Nodes is null || p.Nodes.Any(n => n is null)
            || p.Nodes.Select(n => n.Id).Distinct().Count() != p.Nodes.Count
            || p.Nodes.Any(n => n.ParentId is { } parent && !p.Nodes.Any(candidate => candidate.Id == parent)))
            throw new System.Text.Json.JsonException("Unsupported or invalid cloud project. Local writing is preserved.");
        var old = previous?.Project;
        if (old?.ServerProjectId is { } server && server != p.Id) throw new InvalidOperationException("Cloud project ownership changed; preserve both copies before continuing.");
        var oldNodes = old?.Nodes.ToDictionary(n => n.ServerNodeId ?? n.NodeId) ?? [];
        var ids = p.Nodes.ToDictionary(n => n.Id, n => oldNodes.GetValueOrDefault(n.Id)?.NodeId ?? Guid.NewGuid());
        var oldSections = (previous?.Sections ?? []).Concat(previous?.DeletedSections.Select(s => s.Section) ?? []).ToDictionary(s => s.ServerSectionId ?? s.SectionId);
        return new() { ProjectId = old?.ProjectId ?? Guid.NewGuid(), ServerProjectId = p.Id, ManuscriptId = localId, Version = p.Version >= 3 ? 3 : 2, Synopsis = p.Synopsis,
            PrimaryDocumentId = p.PrimaryDocumentId == document.Id ? localId
                : old?.ServerPrimaryDocumentId == p.PrimaryDocumentId ? old?.PrimaryDocumentId ?? p.PrimaryDocumentId ?? localId : p.PrimaryDocumentId ?? localId,
            ServerPrimaryDocumentId = p.PrimaryDocumentId, ServerMetadataRevision = p.MetadataRevision,
            Title = p.Title, Subtitle = p.Subtitle, AuthorName = p.AuthorName, Language = p.Language, Genre = p.Genre,
            DefaultExportSettingsJson = p.DefaultExportSettingsJson, CoverImageUrl = p.CoverImageUrl, LastPageId = old?.LastPageId, ExtensionData = old?.ExtensionData,
            Nodes = p.Nodes.Select(n => new LocalProjectNode { NodeId = ids[n.Id], ServerNodeId = n.Id,
                ParentId = n.ParentId is { } parent ? ids[parent] : null, SectionId = n.SectionId is { } section ? oldSections.GetValueOrDefault(section)?.SectionId ?? section : null,
                Title = n.Title, NodeType = n.NodeType, OrderIndex = n.OrderIndex, DeletionId = n.DeletionId,
                MetadataJson = n.MetadataJson, Notes = n.Notes, Card = n.Card, Annotations = (n.Annotations ?? []).Select(a => new LocalSceneAnnotation(oldNodes.GetValueOrDefault(n.Id)?.Annotations.FirstOrDefault(x => (x.ServerId ?? x.LocalId) == a.Id)?.LocalId ?? Guid.NewGuid(), a.Id, a)).ToArray(), ExtensionData = oldNodes.GetValueOrDefault(n.Id)?.ExtensionData }).ToArray() };
    }
    internal static bool RemovesProjectNodes(LocalDocument local, SyncSnapshot remote) => local.Project is { } p && remote.Document is { } d
        && (p.Nodes.Any(n => d.Project?.Nodes.All(r => r.Id != (n.ServerNodeId ?? n.NodeId)) != false
            || n.Annotations.Any(a => d.Project?.Nodes.SelectMany(r => r.Annotations ?? []).All(r => r.Id != (a.ServerId ?? a.LocalId)) != false))
            || (p.Synopsis is not null && d.Project?.Synopsis is null));
}
