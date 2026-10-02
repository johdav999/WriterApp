using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

public enum LocalStructureAction
{
    CreateSection, CreatePage, RenameSection, RenamePage, SectionUp, SectionDown,
    PageUp, PageDown, MovePage, DeleteSection, DeletePage, RestoreSection, RestorePage
}

public sealed record LocalStructureChange(LocalStructureAction Action, Guid ItemId = default,
    Guid TargetSectionId = default, string? Title = null);

// Pure aggregate transformation. Persistence/revision checks remain in the document repository.
public static class LocalDocumentStructure
{
    public static LocalDocument Apply(LocalDocument document, LocalStructureChange change, DateTimeOffset now)
    {
        if (document.DeletedAtUtc is not null) throw new InvalidOperationException("Restore this document first.");
        if (document.Project is { } project && change.Action == LocalStructureAction.DeleteSection
            && project.Nodes.Any(n => n.SectionId == change.ItemId))
            throw new InvalidOperationException("This section belongs to a project scene. Use Projects to remove tree items; their writing is retained for recovery.");
        if (document.Project is not null && change.Action is LocalStructureAction.SectionUp or LocalStructureAction.SectionDown)
            throw new InvalidOperationException("Use Projects to reorder scenes in the manuscript.");
        var sections = document.Sections.OrderBy(s => s.OrderIndex).ToList();
        var sectionTrash = document.DeletedSections.ToList();
        var pageTrash = document.DeletedPages.ToList();
        string Title()
        {
            string title = change.Title?.Trim() ?? "";
            if (title.Length is < 1 or > 200) throw new InvalidOperationException("Use a title of 1–200 characters.");
            return title;
        }
        LocalSection Section(Guid id) => sections.SingleOrDefault(s => s.SectionId == id)
            ?? throw new InvalidOperationException("The section is no longer available. Reload the document.");
        LocalSection PageSection() => sections.SingleOrDefault(s => s.Pages.Any(p => p.PageId == change.ItemId))
            ?? throw new InvalidOperationException("The page is no longer available. Reload the document.");
        void Replace(LocalSection section) => sections[sections.FindIndex(s => s.SectionId == section.SectionId)] = section;
        LocalPage NewPage(string title) => new()
        {
            PageId = Guid.NewGuid(), Title = title, OrderIndex = 0, Content = "", ContentFormat = LocalContentFormat.Html,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        switch (change.Action)
        {
            case LocalStructureAction.CreateSection:
                sections.Add(new LocalSection { SectionId = Guid.NewGuid(), Title = Title(), OrderIndex = Next(sections.Select(s => s.OrderIndex)),
                    CreatedAtUtc = now, UpdatedAtUtc = now, Pages = [NewPage("Page 1")] });
                break;
            case LocalStructureAction.CreatePage:
                var target = Section(change.TargetSectionId);
                Replace(target with { Pages = target.Pages.Append(NewPage(Title()) with { OrderIndex = Next(target.Pages.Select(p => p.OrderIndex)) }).ToArray() });
                break;
            case LocalStructureAction.RenameSection:
                Replace(Section(change.ItemId) with { Title = Title() });
                break;
            case LocalStructureAction.RenamePage:
                var renameParent = PageSection();
                Replace(renameParent with { Pages = renameParent.Pages.Select(p => p.PageId == change.ItemId ? p with { Title = Title() } : p).ToArray() });
                break;
            case LocalStructureAction.SectionUp:
            case LocalStructureAction.SectionDown:
                var section = Section(change.ItemId);
                int index = sections.IndexOf(section);
                int neighbor = index + (change.Action == LocalStructureAction.SectionUp ? -1 : 1);
                if (neighbor < 0 || neighbor >= sections.Count) throw new InvalidOperationException("Already at the end of the list.");
                var other = sections[neighbor];
                Replace(section with { OrderIndex = other.OrderIndex });
                Replace(other with { OrderIndex = section.OrderIndex });
                break;
            case LocalStructureAction.PageUp:
            case LocalStructureAction.PageDown:
                var reorderParent = PageSection();
                var pages = reorderParent.Pages.OrderBy(p => p.OrderIndex).ToArray();
                int pageIndex = Array.FindIndex(pages, p => p.PageId == change.ItemId);
                int adjacent = pageIndex + (change.Action == LocalStructureAction.PageUp ? -1 : 1);
                if (adjacent < 0 || adjacent >= pages.Length) throw new InvalidOperationException("Already at the end of the list.");
                int order = pages[pageIndex].OrderIndex;
                pages[pageIndex] = pages[pageIndex] with { OrderIndex = pages[adjacent].OrderIndex };
                pages[adjacent] = pages[adjacent] with { OrderIndex = order };
                Replace(reorderParent with { Pages = pages });
                break;
            case LocalStructureAction.MovePage:
            case LocalStructureAction.DeletePage:
                var source = PageSection();
                if (source.Pages.Count <= 1) throw new InvalidOperationException("Keep at least one page in each section.");
                var page = source.Pages.Single(p => p.PageId == change.ItemId);
                if (change.Action == LocalStructureAction.MovePage)
                {
                    var destination = Section(change.TargetSectionId);
                    if (destination.SectionId == source.SectionId) throw new InvalidOperationException("Choose a different section.");
                    Replace(destination with { Pages = destination.Pages.Append(page with { OrderIndex = Next(destination.Pages.Select(p => p.OrderIndex)) }).ToArray() });
                }
                else pageTrash.Add(new(source.SectionId, page, now));
                Replace(source with { Pages = source.Pages.Where(p => p.PageId != change.ItemId).ToArray() });
                break;
            case LocalStructureAction.DeleteSection:
                if (sections.Count <= 1) throw new InvalidOperationException("Keep at least one section in the document.");
                var removed = Section(change.ItemId);
                sectionTrash.Add(new(removed, now));
                sections.Remove(removed);
                break;
            case LocalStructureAction.RestoreSection:
                var entry = sectionTrash.SingleOrDefault(x => x.Section.SectionId == change.ItemId)
                    ?? throw new InvalidOperationException("This section is no longer in trash.");
                var restored = entry.Section;
                if (restored.Pages.Count == 0) restored = restored with { Pages = [NewPage("Page 1")] };
                // Append on restore; unrelated siblings keep their existing order values.
                sections.Add(restored with { OrderIndex = Next(sections.Select(s => s.OrderIndex)) });
                sectionTrash.Remove(entry);
                break;
            case LocalStructureAction.RestorePage:
                var pageEntry = pageTrash.SingleOrDefault(x => x.Page.PageId == change.ItemId)
                    ?? throw new InvalidOperationException("This page is no longer in trash.");
                var restoreParent = Section(change.TargetSectionId == Guid.Empty ? pageEntry.SectionId : change.TargetSectionId);
                Replace(restoreParent with { Pages = restoreParent.Pages.Append(pageEntry.Page with { OrderIndex = Next(restoreParent.Pages.Select(p => p.OrderIndex)) }).ToArray() });
                pageTrash.Remove(pageEntry);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        if (sections.Count > 100 || sections.Sum(s => s.Pages.Count) > 1000)
            throw new InvalidOperationException("A document supports up to 100 sections and 1,000 pages.");
        return document with { Sections = sections.OrderBy(s => s.OrderIndex).Select(s => s with { Pages = s.Pages.OrderBy(p => p.OrderIndex).ToArray() }).ToArray(),
            Project = document.Project is { } localProject && change.Action == LocalStructureAction.RenameSection
                ? localProject with { Nodes = localProject.Nodes.Select(n => n.SectionId == change.ItemId ? n with { Title = Title() } : n).ToArray() } : document.Project,
            DeletedSections = sectionTrash, DeletedPages = pageTrash };
    }

    public static Guid? SelectPage(LocalDocument before, LocalDocument after, Guid? current)
    {
        var pages = after.Sections.OrderBy(s => s.OrderIndex).SelectMany(s => s.Pages.OrderBy(p => p.OrderIndex)).ToArray();
        if (pages.Any(p => p.PageId == current)) return current;
        var old = before.Sections.OrderBy(s => s.OrderIndex).SelectMany(s => s.Pages.OrderBy(p => p.OrderIndex)).ToArray();
        int index = Array.FindIndex(old, p => p.PageId == current);
        return pages.Length == 0 ? null : pages[Math.Clamp(index, 0, pages.Length - 1)].PageId;
    }

    private static int Next(IEnumerable<int> orders) => checked(orders.DefaultIfEmpty(-1).Max() + 1);

    internal static LocalDocument DetachIdentities(LocalDocument source, bool preserveProject = false)
    {
        var ids = source.Sections.Concat(source.DeletedSections.Select(x => x.Section)).Select(s => s.SectionId)
            .Concat(source.DeletedPages.Select(x => x.SectionId)).Distinct().ToDictionary(id => id, _ => Guid.NewGuid());
        LocalPage Page(LocalPage p) => p with { PageId = Guid.NewGuid(), ServerPageId = null };
        LocalSection Section(LocalSection s) => s with { SectionId = ids[s.SectionId], ServerSectionId = null, Pages = s.Pages.Select(Page).ToArray() };
        var nodeIds = source.Project?.Nodes.ToDictionary(n => n.NodeId, _ => Guid.NewGuid());
        var project = preserveProject && source.Project is { } p ? p with { ProjectId = Guid.NewGuid(), ServerProjectId = null, LastPageId = null,
            PrimaryDocumentId = null, ServerPrimaryDocumentId = null, ServerMetadataRevision = null, MetadataRevision = 1, MetadataDirty = false,
            Nodes = p.Nodes.Select(n => n with { Annotations = n.Annotations.Select(a => { var id = Guid.NewGuid(); return new LocalSceneAnnotation(id, null, a.Value with { Id = id }); }).ToArray(), NodeId = nodeIds![n.NodeId], ServerNodeId = null,
                ParentId = n.ParentId is { } parent ? nodeIds![parent] : null, SectionId = n.SectionId is { } section ? ids[section] : null }).ToArray() } : null;
        return source with { Project = project, Sections = source.Sections.Select(Section).ToArray(),
            DeletedSections = source.DeletedSections.Select(x => x with { Section = Section(x.Section) }).ToArray(),
            DeletedPages = source.DeletedPages.Select(x => x with { SectionId = ids[x.SectionId], Page = Page(x.Page) }).ToArray() };
    }
}
