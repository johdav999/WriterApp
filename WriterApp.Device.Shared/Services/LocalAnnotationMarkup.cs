using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

public sealed record LocalTextAnnotation(Guid Id, string Quote, string Kind, string Status, string Content);

public static class LocalAnnotationMarkup
{
    public static IReadOnlyList<LocalTextAnnotation> ForPage(LocalDocument document, Guid pageId)
    {
        var section = document.Sections.FirstOrDefault(s => s.Pages.Any(p => p.PageId == pageId));
        var scene = document.Project?.Nodes.FirstOrDefault(n => n.NodeType == "scene" && n.DeletionId is null && n.SectionId == section?.SectionId);
        if (section is null || scene is null || scene.Annotations.Count == 0) return [];
        string sceneText = LocalPlanning.SceneText(document, scene);
        string pageText = LocalDocumentPreview.PlainText(section.Pages.Single(p => p.PageId == pageId));
        // Revalidate current writing, including undo and restored legacy quotes.
        // A saved detached flag describes an older snapshot, not the current match.
        return scene.Annotations.Where(a => LocalPlanning.UniqueQuote(sceneText, a.Value.AnchorText)
                && pageText.Contains(a.Value.AnchorText, StringComparison.Ordinal))
            .Select(a => new LocalTextAnnotation(a.LocalId, a.Value.AnchorText, a.Value.Kind, a.Value.Status, a.Value.Content)).ToArray();
    }

    public static Guid? PageFor(LocalDocument document, Guid annotationId)
    {
        var scene = document.Project?.Nodes.FirstOrDefault(n => n.NodeType == "scene" && n.DeletionId is null
            && n.Annotations.Any(a => a.LocalId == annotationId));
        var annotation = scene?.Annotations.FirstOrDefault(a => a.LocalId == annotationId)?.Value;
        if (scene is null || annotation is null || !LocalPlanning.UniqueQuote(LocalPlanning.SceneText(document, scene), annotation.AnchorText)) return null;
        return document.Sections.FirstOrDefault(s => s.SectionId == scene.SectionId)?.Pages
            .FirstOrDefault(p => LocalDocumentPreview.PlainText(p).Contains(annotation.AnchorText, StringComparison.Ordinal))?.PageId;
    }
}
