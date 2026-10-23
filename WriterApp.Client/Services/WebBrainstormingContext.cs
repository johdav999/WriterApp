using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using WriterApp.Shared;
using WriterApp.Shared.Brainstorming;
using WriterApp.Shared.Canon;

namespace WriterApp.Client.Services;

public sealed class WebBrainstormingTextProjector(IJSRuntime js) : IBrainstormingTextProjector
{
    public async Task<BrainstormingProjectedPage> ProjectAsync(BrainstormingStoryPage page, CancellationToken ct) =>
        new(page.Id, BrainstormingValidation.Fingerprint(page.Content), await js.InvokeAsync<string>("tiptapEditor.brainstormingPlainText", ct, page.Content, page.Format));
}

/// <summary>Checked read-only context capture. Pending editor HTML stays local; no save/provider request is made.</summary>
public sealed class WebBrainstormingContext(HttpClient http, WebCheckedAi checkedAi, WebBrainstormingSessions conversations, WebBrainstormingTextProjector projector)
{
    public async Task<BrainstormingSupportingPage> ListSupportingAsync(Guid document, Guid? after = null, CancellationToken ct = default)
    {
        var lease = await checkedAi.Capture(document, ct: ct);
        using var response = await http.GetAsync($"api/brainstorming/sources?after={after}&limit=25", ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Sign in to list available supporting documents.");
        var page = await WebAiSources.Read<BrainstormingSupportingPage>(response.Content, ct);
        if (page.Version != 1 || page.Items.Count > 25 || page.Items.Any(d => d.Id == Guid.Empty || d.Title.Length > 200) || page.NextAfter == Guid.Empty) throw new InvalidDataException("Invalid supporting document list.");
        await checkedAi.Confirm(lease, ct); return page;
    }
    public async Task<BrainstormingStorySnapshot> ReadAsync(Guid document, Guid? project = null, CancellationToken ct = default)
    {
        using var response = await ReadSource($"api/brainstorming/sources/{document}?projectId={project}", ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(response.StatusCode switch {
            System.Net.HttpStatusCode.Unauthorized => "Sign in again to prepare story context. Your writing and conversation are retained.",
            System.Net.HttpStatusCode.Forbidden => "Brainstorm requires owner or editor access to this manuscript. Your writing and conversation are retained.",
            System.Net.HttpStatusCode.Conflict => "Shared writing or story context is still changing. Wait for writing to save, then reopen Brainstorm.",
            _ => "Story context is unavailable. Check manuscript access and saved writing, then reopen Brainstorm."
        });
        if (response.Content.Headers.ContentLength > 8_000_000) throw new InvalidDataException("Story source exceeds the transport limit. Select a smaller source.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct); using var buffer = new MemoryStream(); var block = new byte[8192];
        int read; while ((read = await stream.ReadAsync(block, ct)) > 0) { if (buffer.Length + read > 8_000_000) throw new InvalidDataException("Story source exceeds the transport limit."); buffer.Write(block, 0, read); }
        var source = JsonSerializer.Deserialize<BrainstormingStorySnapshot>(buffer.ToArray(), BrainstormingValidation.Json) ?? throw new InvalidDataException("Missing story context.");
        BrainstormingStorySources.Validate(source);
        if (source.Outline.DocumentId != document || project is not null && source.Outline.ProjectId != project) throw new InvalidDataException("Backend returned another story document/project.");
        return source;
    }
    private async Task<HttpResponseMessage> ReadSource(string url, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++) {
            var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode != System.Net.HttpStatusCode.Conflict || attempt >= 19) return response;
            try {
                var error = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (!error.TryGetProperty("code", out var code) || code.GetString() != "collaboration_projection_pending") return response;
            } catch (JsonException) { return response; }
            response.Dispose(); await Task.Delay(200, ct);
        }
    }
    public async Task<BrainstormingPreparation> PrepareAsync(Guid document, BrainstormingTargetQuery query, IReadOnlyList<BrainstormingContextChoice> choices,
        IReadOnlyList<Guid> supportingDocuments, int budget, Func<CancellationToken, Task<(Guid PageId, string Html)?>> capturePending,
        BrainstormingScope? sessionScope = null, BrainstormingSession? previous = null, CancellationToken ct = default)
    {
        var pending = await capturePending(ct); var lease = await checkedAi.Capture(document, ct: ct); var source = await ReadAsync(document, ct: ct);
        if (sessionScope is { ProjectId:null } && source.Outline.ProjectId is not null)
            throw new InvalidOperationException("This conversation predates the manuscript outline. Start a new conversation for the current outline; saved drafts and recovery history are retained.");
        if (source.Outline.DocumentVersion != lease.Source.DocumentVersion) throw new InvalidDataException("Source revision changed. Refresh story context.");
        bool dirty = false;
        if (pending is { } edit) {
            var page = source.Pages.SingleOrDefault(p => p.Id == edit.PageId) ?? throw new InvalidDataException("The active page belongs to another manuscript.");
            dirty = page.Content != edit.Html;
            if (dirty) source = source with { Pages = source.Pages.Select(p => p.Id == edit.PageId ? p with { Content = edit.Html, Revision = "pending:" + BrainstormingValidation.Fingerprint(edit.Html), Freshness = BrainstormingFreshness.PendingEditorChanges } : p).ToArray(),
                ServerGenerationEligible = false, Limitation = "Pending editor writing is included in this local review. Save it and refresh context before server generation; no older cloud text was substituted." };
        }
        var refs = source.References.ToList();
        foreach (var kind in Enum.GetValues<CanonKind>()) {
            var type = Enum.Parse<BrainstormingSourceKind>(kind.ToString()); var id = BrainstormingStorySources.Identity(document, type.ToString());
            if (!choices.Any(c => c.SourceId == id && c.Enabled)) continue;
            using var response = await http.GetAsync($"api/documents/{document}/bibles/{kind.ToString().ToLowerInvariant()}/device?expectedDocumentVersion={Uri.EscapeDataString(lease.Source.DocumentVersion)}", ct);
            DeviceBibleSnapshot? snapshot = null;
            if (response.IsSuccessStatusCode) snapshot = await WebAiSources.Read<DeviceBibleSnapshot>(response.Content, ct);
            else if ((int)response.StatusCode == 401) throw new InvalidOperationException("Sign in again to review story canon.");
            refs.RemoveAll(r => r.Id == id); refs.Add(BrainstormingStorySources.Canon(document, type, snapshot, document, lease.Source.DocumentVersion, dirty));
        }
        if (supportingDocuments.Count > 10 || supportingDocuments.Distinct().Count() != supportingDocuments.Count || supportingDocuments.Contains(document)) throw new InvalidDataException("Select at most ten distinct supporting documents.");
        var supportingLeases = new List<WebCheckedAi.Lease>();
        foreach (var id in supportingDocuments) {
            var supportLease = await checkedAi.Capture(id, ct: ct); var supporting = await ReadAsync(id, ct: ct);
            if (supporting.Outline.DocumentVersion != supportLease.Source.DocumentVersion) throw new InvalidDataException("A selected supporting source changed.");
            refs.Add(BrainstormingStorySources.Supporting(supporting, await BrainstormingStorySources.ProjectAsync(supporting, projector, ct))); supportingLeases.Add(supportLease);
        }
        source = BrainstormingStorySources.WithReferences(source, refs);
        var projection = await BrainstormingStorySources.ProjectAsync(source, projector, ct);
        await checkedAi.Confirm(lease, ct); foreach (var support in supportingLeases) await checkedAi.Confirm(support, ct);
        if (pending != await capturePending(ct)) throw new InvalidDataException("Pending writing changed during capture. Refresh; your editor text is retained.");
        var scope = sessionScope ?? await conversations.ScopeAsync(document, source.Outline.ProjectId, ct: ct);
        if (scope.DocumentId != document || scope.AccountKey != BrainstormingValidation.Fingerprint(lease.Account ?? "") || scope.BackendKey != BrainstormingValidation.Fingerprint(lease.Backend)) throw new InvalidDataException("Review account/backend changed.");
        ct.ThrowIfCancellationRequested(); return BrainstormingContextPolicy.Prepare(scope, source, projection, query, choices, budget, previous);
    }
}
