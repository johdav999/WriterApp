using System.Text.Json;
using WriterApp.Application.AI;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;

namespace WriterApp.Device.Shared.Services;

public sealed record TranslationPageCapture(IReadOnlyList<TranslationRun> Runs);
public sealed record LocalTranslationPrepared(LocalDocument Source, Guid SectionId, TranslationStructure Structure, DeviceAiPrepared Request);

public static class LocalTranslation
{
    public static LocalTranslationPrepared Prepare(LocalDocument source, Guid sectionId, string scope, string language,
        IReadOnlyDictionary<Guid, TranslationPageCapture> captured, string sourceLanguage = "auto", string style = "natural")
    {
        if (source.ServerDocumentId is null || source.SyncState != LocalSyncState.Synced || string.IsNullOrEmpty(source.ServerVersion)
            || source.DeletedAtUtc is not null) throw new InvalidOperationException("Sign in, synchronize and resolve conflicts before translating.");
        var sections = Scope(source, sectionId, scope);
        if (source.Project?.Nodes.Any(n => n.SectionId is { } id && sections.Any(s => s.SectionId == id) && n.Annotations.Count != 0) == true)
            throw new InvalidOperationException("These pages have anchored annotations. Translate a selection or an unannotated section; whole-scope annotation mapping is not supported yet.");
        if (captured.Count != sections.Sum(s => s.Pages.Count)) throw new InvalidDataException("Capture every intended page before translating.");
        var structure = new TranslationStructure(1, source.ServerDocumentId.Value, scope, language, sections.Select(s =>
            new TranslationSection(s.ServerSectionId ?? throw new InvalidOperationException("Synchronize the section first."), s.Pages.OrderBy(p => p.OrderIndex).Select(p =>
                new TranslationPage(p.ServerPageId ?? throw new InvalidOperationException("Synchronize the page first."),
                    captured.TryGetValue(p.PageId, out var value) ? value.Runs : throw new InvalidDataException("A translation page is missing."))).ToArray())).ToArray());
        TranslationStructures.Validate(structure);
        if (sourceLanguage != "auto" && !WriterApp.Shared.Localization.TranslationLanguages.All.Any(l => l.Code == sourceLanguage)
            || style is not ("literal" or "natural" or "formal" or "informal")) throw new InvalidOperationException("Choose a supported source language and translation style.");
        string json = TranslationStructures.Serialize(structure);
        if (json.Length > 100000) throw new InvalidDataException("Translation markers exceed the safe limit. Translate a smaller scope.");
        var active = source.Sections.Single(s => s.SectionId == sectionId); var first = active.Pages.OrderBy(p => p.OrderIndex).First();
        var request = new AiActionExecuteRequestDto(source.ServerDocumentId, active.ServerSectionId, first.ServerPageId,
            null, null, null, null, null, new() { [TranslationStructures.Parameter] = json, ["target_language"] = language,
                ["source_language"] = sourceLanguage, ["style"] = style }, source.ServerVersion);
        return new(source, sectionId, structure, new("translate." + scope, request, DeviceAiAction.Translate, first.PageId, first.Content, 0, 0, "", "analysis"));
    }
    public static IReadOnlyList<LocalSection> Scope(LocalDocument source, Guid sectionId, string scope) => scope switch {
        "section" => [source.Sections.Single(s => s.SectionId == sectionId)],
        "document" => source.Sections.OrderBy(s => s.OrderIndex).ToArray(),
        _ => throw new InvalidOperationException("Choose section or document translation.") };
    public static void RequireFresh(LocalDocument current, LocalDocument source)
    {
        if (current.DocumentId != source.DocumentId || current.LocalRevision != source.LocalRevision || current.ServerVersion != source.ServerVersion
            || current.SyncState != LocalSyncState.Synced || current.DeletedAtUtc is not null || Signature(current) != Signature(source))
            throw new InvalidOperationException("The source changed after translation. Synchronize and generate a new preview.");
    }
    // Authored data only, for crash recovery and guarding copy undo; sync timestamps cannot erase recovery evidence.
    public static string Signature(LocalDocument d) => JsonSerializer.Serialize(new { d.DocumentId, d.Title, d.LanguageCode,
        Sections = d.Sections.Select(s => new { s.SectionId, s.Title, s.NarrativePurpose, s.OrderIndex, s.LanguageCode,
            Pages = s.Pages.Select(p => new { p.PageId, p.Title, p.OrderIndex, p.Content, p.ContentFormat, p.ExtensionData }), s.ExtensionData }),
        Project = d.Project is { } project ? new { project.ProjectId, project.ManuscriptId, project.Title, project.Subtitle, project.AuthorName,
            project.Language, project.Genre, project.DefaultExportSettingsJson, project.CoverImageUrl,
            Nodes = project.Nodes.Select(n => n with { ServerNodeId = null }), project.Synopsis } : null,
        d.DeletedSections, d.DeletedPages });
    public static LocalDocument Preview(LocalTranslationPrepared prepared, IReadOnlyDictionary<Guid, string> html, string mode)
    {
        var source = prepared.Source; var scoped = Scope(source, prepared.SectionId, prepared.Structure.Scope);
        if (html.Count != scoped.Sum(s => s.Pages.Count) || scoped.SelectMany(s => s.Pages).Any(p => !html.ContainsKey(p.PageId)))
            throw new InvalidDataException("The complete translated page mapping is required.");
        var translated = source with { Sections = source.Sections.Select(s => scoped.Any(x => x.SectionId == s.SectionId)
            ? s with { LanguageCode = prepared.Structure.TargetLanguage, Pages = s.Pages.Select(p => p with { Content = html[p.PageId], ContentFormat = LocalContentFormat.Html }).ToArray() } : s).ToArray() };
        if (mode == "replace") return prepared.Structure.Scope == "document" ? translated with { LanguageCode = prepared.Structure.TargetLanguage } : translated;
        if (mode == "duplicate-document" && prepared.Structure.Scope == "document") {
            var now = DateTimeOffset.UtcNow; var id = Guid.NewGuid();
            var copy = LocalDocumentStructure.DetachIdentities(translated, preserveProject: true) with { DocumentId = id,
                Title = CopyTitle(source.Title, prepared.Structure.TargetLanguage), LanguageCode = prepared.Structure.TargetLanguage,
                ServerDocumentId = null, ServerProjectId = null, ServerVersion = null, LastSyncedAtUtc = null, SyncState = LocalSyncState.LocalOnly,
                LocalRevision = 1, CreatedAtUtc = now, UpdatedAtUtc = now };
            if (copy.Project is { } project) copy = copy with { Project = project with { Title = copy.Title, Language = prepared.Structure.TargetLanguage,
                ManuscriptId = id, PrimaryDocumentId = copy.Kind == "manuscript" ? id : null } };
            LocalDocumentCodec.Validate(copy); return copy;
        }
        if (mode == "duplicate-section" && prepared.Structure.Scope == "section") {
            var s = translated.Sections.Single(s => s.SectionId == prepared.SectionId); var id = Guid.NewGuid();
            var added = s with { SectionId = id, ServerSectionId = null, Title = CopyTitle(s.Title, prepared.Structure.TargetLanguage),
                OrderIndex = checked(source.Sections.Max(s => s.OrderIndex) + 1), Pages = s.Pages.Select(p => p with { PageId = Guid.NewGuid(), ServerPageId = null }).ToArray() };
            var result = source with { Sections = source.Sections.Append(added).ToArray() };
            if (source.Project is { } project) {
                var original = project.Nodes.SingleOrDefault(n => n.SectionId == s.SectionId && n.DeletionId is null);
                if (original is not null) result = result with { Project = project with { Nodes = project.Nodes.Append(original with {
                    NodeId = Guid.NewGuid(), ServerNodeId = null, SectionId = id, Title = added.Title, Annotations = [],
                    OrderIndex = checked(project.Nodes.Where(n => n.ParentId == original.ParentId).Max(n => n.OrderIndex) + 1) }).ToArray() } };
            }
            LocalDocumentCodec.Validate(result); return result;
        }
        throw new InvalidOperationException("This application choice does not match the translation scope.");
    }
    private static string CopyTitle(string title, string language) {
        string suffix = " (" + WriterApp.Shared.Localization.TranslationLanguages.GetDisplayNameOrValue(language) + ")";
        return title[..Math.Min(title.Length, 200 - suffix.Length)] + suffix;
    }
}

public sealed class LocalTranslationActions(LocalDocumentRepository documents, LocalAiStore history, DeviceAiService ai)
{
    public async Task ApplyAsync(LocalAiHistory entry, DeviceAiProposal proposal, CancellationToken ct = default)
    {
        ai.RequireCurrentAccount(proposal); ct.ThrowIfCancellationRequested();
        if (proposal.Prepared.Request.DocumentId != entry.Before.ServerDocumentId
            || proposal.Prepared.Request.ExpectedDocumentVersion != entry.Before.ServerVersion
            || proposal.Prepared.Key != entry.Action || proposal.ProposedText != entry.Proposed)
            throw new InvalidOperationException("The proposal does not belong to this reviewed translation source. Generate a new preview.");
        var current = await documents.LoadAsync(entry.DocumentId, ct) ?? throw new IOException("Source document unavailable.");
        LocalTranslation.RequireFresh(current, entry.Before);
        if (entry.Version != 2 || !entry.Target.StartsWith("Translation:", StringComparison.Ordinal) || entry.After is null)
            throw new InvalidDataException("The complete recovery preview is missing.");
        await history.SaveHistoryAsync(entry with { Status = "Applying" }, ct);
        ai.RequireCurrentAccount(proposal); ct.ThrowIfCancellationRequested();
        if (entry.Target == "Translation:duplicate-document") await documents.CreateTranslationCopyAsync(entry.After, ct);
        else await documents.SaveAsync(entry.After, ct);
        await history.SaveHistoryAsync(entry with { Status = "Applied" }, CancellationToken.None);
    }
    /// <summary>Resume an explicitly approved intent after restart; never regenerate or guess missing pages.</summary>
    public static async Task ResumeAsync(LocalDocumentRepository documents, LocalAiStore history, LocalAiHistory entry, CancellationToken ct)
    {
        if (entry.Status != "Applying" || entry.Version != 2 || !entry.Target.StartsWith("Translation:", StringComparison.Ordinal) || entry.After is null)
            throw new InvalidOperationException("No approved translation save is pending.");
        var current = await documents.LoadAsync(entry.DocumentId, ct) ?? throw new IOException("Source unavailable.");
        if (entry.Target == "Translation:duplicate-document") {
            var copy = await documents.LoadAsync(entry.After.DocumentId, ct);
            if (copy is null) { LocalTranslation.RequireFresh(current, entry.Before); await documents.CreateTranslationCopyAsync(entry.After, ct); }
            else if (LocalTranslation.Signature(copy) != LocalTranslation.Signature(entry.After)) throw new InvalidOperationException("The translated copy has later edits. Keep it and recover the original separately.");
        } else if (LocalTranslation.Signature(current) != LocalTranslation.Signature(entry.After)) {
            LocalTranslation.RequireFresh(current, entry.Before); await documents.SaveAsync(entry.After, ct);
        }
        await history.SaveHistoryAsync(entry with { Status = "Applied" }, CancellationToken.None);
    }
}
