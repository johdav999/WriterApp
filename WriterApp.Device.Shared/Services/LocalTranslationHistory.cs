using System.Text.Json;
using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

public static class LocalTranslationHistory
{
    public static LocalDocument Change(LocalDocument current, LocalAiHistory entry, bool redo)
    {
        var after = entry.After ?? throw new InvalidDataException("Translation recovery result is missing.");
        var from = redo ? entry.Before : after; var to = redo ? after : entry.Before;
        bool retry = entry.Status is "Undoing" or "Redoing";
        T Replace<T>(T actual, T expected, T desired) {
            if (EqualityComparer<T>.Default.Equals(expected, desired)) return actual;
            if (retry && EqualityComparer<T>.Default.Equals(actual, desired)) return actual;
            if (!EqualityComparer<T>.Default.Equals(actual, expected)) throw new InvalidOperationException("Translated writing has later changes. Recover the original as a copy.");
            return desired;
        }
        if (entry.Target == "Translation:replace") {
            var sections = current.Sections.ToArray();
            foreach (var oldSection in from.Sections) {
                var desired = to.Sections.Single(s => s.SectionId == oldSection.SectionId);
                bool affected = oldSection.LanguageCode != desired.LanguageCode || oldSection.Pages.Any(p => {
                    var d = desired.Pages.Single(x => x.PageId == p.PageId); return p.Content != d.Content || p.ContentFormat != d.ContentFormat; });
                if (!affected) continue;
                int index = Array.FindIndex(sections, s => s.SectionId == oldSection.SectionId);
                if (index < 0 || sections[index].OrderIndex != oldSection.OrderIndex
                    || !sections[index].Pages.OrderBy(p => p.OrderIndex).Select(p => (p.PageId, p.OrderIndex))
                        .SequenceEqual(oldSection.Pages.OrderBy(p => p.OrderIndex).Select(p => (p.PageId, p.OrderIndex))))
                    throw new InvalidOperationException("Translated pages moved or changed structure. Recover the original as a copy.");
                var actual = sections[index];
                sections[index] = actual with { LanguageCode = Replace(actual.LanguageCode, oldSection.LanguageCode, desired.LanguageCode),
                    Pages = actual.Pages.Select(p => {
                        var before = oldSection.Pages.Single(x => x.PageId == p.PageId); var result = desired.Pages.Single(x => x.PageId == p.PageId);
                        var text = Replace((p.Content, p.ContentFormat), (before.Content, before.ContentFormat), (result.Content, result.ContentFormat));
                        return p with { Content = text.Item1, ContentFormat = text.Item2 };
                    }).ToArray() };
            }
            return current with { Sections = sections, LanguageCode = Replace(current.LanguageCode, from.LanguageCode, to.LanguageCode) };
        }
        if (entry.Target == "Translation:duplicate-section") {
            var added = after.Sections.Single(s => entry.Before.Sections.All(b => b.SectionId != s.SectionId));
            var actual = current.Sections.SingleOrDefault(s => s.SectionId == added.SectionId);
            static string Section(LocalSection s) => JsonSerializer.Serialize(new { s.SectionId, s.Title, s.NarrativePurpose, s.LanguageCode, s.OrderIndex,
                Pages = s.Pages.Select(p => new { p.PageId, p.Title, p.OrderIndex, p.Content, p.ContentFormat, p.ExtensionData }), s.ExtensionData });
            if (actual is not null && Section(actual) != Section(added) || !retry && (redo ? actual is not null : actual is null))
                throw new InvalidOperationException("The translated section has later changes or is unavailable. Recover the original as a copy.");
            var nodes = after.Project?.Nodes.Where(n => entry.Before.Project!.Nodes.All(b => b.NodeId != n.NodeId)).ToArray() ?? [];
            var currentNodes = current.Project?.Nodes.ToList();
            if (nodes.Length != 0 && (currentNodes is null || current.Project?.ProjectId != entry.Before.Project?.ProjectId))
                throw new InvalidOperationException("The original planning project is unavailable. Recover a copy instead.");
            foreach (var node in nodes) {
                var existing = currentNodes?.SingleOrDefault(n => n.NodeId == node.NodeId);
                if (existing is not null && JsonSerializer.Serialize(existing with { ServerNodeId = null }) != JsonSerializer.Serialize(node with { ServerNodeId = null }))
                    throw new InvalidOperationException("The translated scene has later planning changes. Recover the original as a copy.");
                if (redo && existing is null) {
                    if (currentNodes!.Any(n => n.ParentId == node.ParentId && n.OrderIndex == node.OrderIndex)) throw new InvalidOperationException("The translated scene's position is occupied. Recover a copy instead.");
                    currentNodes!.Add(node);
                } else if (!redo && existing is not null) currentNodes!.Remove(existing);
            }
            if (redo && actual is null && current.Sections.Any(s => s.OrderIndex == added.OrderIndex))
                throw new InvalidOperationException("The translated section's position is occupied. Recover a copy instead.");
            return current with { Sections = redo ? actual is null ? current.Sections.Append(added).ToArray() : current.Sections
                : current.Sections.Where(s => s.SectionId != added.SectionId).ToArray(),
                Project = current.Project is { } project ? project with { Nodes = currentNodes!.ToArray() } : null };
        }
        throw new InvalidDataException("Unsupported translation history target.");
    }
}
