using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Quality;

namespace WriterApp.Device.Shared.Services;

public sealed class DeviceQualityDismissalApi(HttpClient http)
{
    public async Task<QualityDismissalReceipt> ExchangeAsync(Guid page, QualityDismissalRequest value, long generation, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/pages/{page}/quality-checks/device-decisions") { Content = JsonContent.Create(value) };
        request.Options.Set(DeviceAuthenticatedHandler.ExpectedAccountGeneration, generation);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Quality decision synchronization failed.", null, response.StatusCode);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var bytes = new MemoryStream(); byte[] buffer = new byte[4096]; int count;
        while ((count = await stream.ReadAsync(buffer, ct)) > 0)
        {
            if (bytes.Length + count > 100_000) throw new InvalidDataException("Quality decision response is too large.");
            bytes.Write(buffer, 0, count);
        }
        return JsonSerializer.Deserialize<QualityDismissalReceipt>(bytes.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web)
            { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow }) ?? throw new InvalidDataException("Empty quality decision response.");
    }
}

public sealed record LocalQualityDecisionView(IReadOnlyList<LocalQualityDecision> Items, IReadOnlySet<string> Hidden, string Message);

/// <summary>Serialized local decisions survive failure; only verified, current full-page findings exchange with the server.</summary>
public sealed class DeviceQualityDismissals(LocalQualityDismissalStore store, DeviceQualityDismissalApi api,
    LocalDocumentRepository documents, DeviceAccountService account, DeviceConnectivity connectivity, DeviceHostOptions host)
{
    public static string SourceHash(LocalQualityAnalysis analysis)
    {
        string hash = QualityDismissalIdentity.Source(analysis.PlainText, analysis.Glossary?.Terms ?? []);
        return FullPage(analysis) ? hash : QualityDismissalIdentity.Hash(hash + $":selection:{analysis.From}:{analysis.To}");
    }
    private static bool FullPage(LocalQualityAnalysis a) => a.From == 0 && a.To == a.PlainText.Length;
    private string Scope => LocalBibleStore.ScopeKey(host.ApiBaseAddress, account.IsSignedIn && !string.IsNullOrWhiteSpace(account.AccountId)
        ? "account:" + account.AccountId : "anonymous");
    public Task<LocalQualityDecisionView> LoadAsync(LocalQualityAnalysis analysis, CancellationToken ct = default) => Run(analysis, null, null, ct);
    public Task<LocalQualityDecisionView> DismissAsync(LocalQualityAnalysis analysis, string key, CancellationToken ct = default) => Run(analysis, key, null, ct);
    public Task<LocalQualityDecisionView> RestoreAsync(LocalQualityAnalysis analysis, string id, CancellationToken ct = default) => Run(analysis, null, id, ct);

    private async Task<LocalQualityDecisionView> Run(LocalQualityAnalysis a, string? dismiss, string? restore, CancellationToken ct)
    {
        long generation = account.Generation; string scope = Scope;
        await store.Gate.WaitAsync(ct);
        try
        {
            async Task<LocalDocument> Check()
            {
                ct.ThrowIfCancellationRequested();
                if (generation != account.Generation || scope != Scope) throw new OperationCanceledException("The account changed.", ct);
                var current = await documents.LoadAsync(a.Source.DocumentId, ct) ?? throw new IOException("Manuscript unavailable.");
                LocalQualityChecks.RequireFresh(current, a);
                if (current.ServerDocumentId != a.Source.ServerDocumentId) throw new InvalidOperationException("The document mapping changed. Check again.");
                return current;
            }
            var current = await Check(); var page = current.Sections.SelectMany(s => s.Pages).Single(p => p.PageId == a.PageId);
            var journal = await store.ReadAsync(scope, current.DocumentId, page.PageId, ct);
            string source = SourceHash(a);
            bool Maps(LocalQualityDecision d) => (d.ServerDocumentId is null || d.ServerDocumentId == current.ServerDocumentId)
                && (d.ServerPageId is null || d.ServerPageId == page.ServerPageId);
            var items = journal.Items.ToList();
            if (dismiss is not null)
            {
                var issue = a.Issues.Single(i => i.IssueKey == dismiss);
                string id = LocalQualityDismissalStore.DecisionId(source, dismiss, current.ServerDocumentId, page.ServerPageId);
                items.RemoveAll(d => Maps(d) && d.SourceHash == source && d.IssueKey == dismiss);
                items.Add(new(id, source, dismiss, issue.RuleId, issue.Message, issue.AnchorText is { Length: > 1000 } anchor ? anchor[..1000] : issue.AnchorText,
                    true, FullPage(a), FullPage(a) ? "Pending" : "Local", current.ServerDocumentId, page.ServerPageId));
            }
            if (restore is not null)
            {
                int index = items.FindIndex(d => d.Id == restore && Maps(d));
                if (index < 0) throw new InvalidOperationException("This decision is unavailable for the current document mapping.");
                var item = items[index];
                // Retain an inactive full-page restore until that exact source can map again.
                // This also prevents a later source reversion from reimporting its old server dismissal.
                bool pending = item.Status != "Local";
                items[index] = item with { Dismissed = false, Pending = pending,
                    Status = pending ? item.SourceHash == source && FullPage(a) ? "Pending" : "Unmapped" : "Local" };
            }
            items.RemoveAll(d => !d.Dismissed && !d.Pending);
            await Check();
            if (dismiss is not null || restore is not null) await store.SaveAsync(journal with { Items = items }, ct);
            string message = "Decisions saved locally. Check again to synchronize after reconnecting.";
            var eligible = items.Where(d => Maps(d) && d.SourceHash == source && a.Issues.Any(i => i.IssueKey == d.IssueKey)).ToArray();
            if (!FullPage(a)) message = "Selection decisions stay local. Full-page checks require their own exact finding identity.";
            else if (!account.IsSignedIn || string.IsNullOrWhiteSpace(account.AccountId)) message = "Signed-out decisions stay local and are separate from signed-in accounts.";
            else if (current.SyncState != LocalSyncState.Synced || current.ServerDocumentId is null || page.ServerPageId is null
                || a.Glossary?.Availability != GlossaryAvailability.Verified)
            {
                message = "Unmapped decisions stay local. Synchronize the document and refresh its glossary with a full-page check to verify a mapping.";
                foreach (var d in eligible.Where(d => d.Pending)) items[items.IndexOf(d)] = d with { Status = "Unmapped" };
            }
            else if (connectivity.IsOnline)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
                try
                {
                    var pending = eligible.Where(d => d.Pending).Select(d => new QualityDismissalDecision(d.IssueKey, d.Dismissed)).ToArray();
                    var receipt = await api.ExchangeAsync(page.ServerPageId.Value, new(1, current.ServerDocumentId.Value, source, pending), generation, timeout.Token);
                    await Check();
                    if (receipt.Version != 1 || receipt.DocumentId != current.ServerDocumentId || receipt.PageId != page.ServerPageId || receipt.SourceHash != source
                        || receipt.Decisions is null || receipt.Decisions.Count > 200 || receipt.Decisions.Any(d => d is null)
                        || receipt.LegacyIssueKeys is { } legacy && (legacy.Count > 200 || legacy.Distinct().Count() != legacy.Count || legacy.Any(k => !a.Issues.Any(i => i.IssueKey == k)))
                        || !receipt.Decisions.Select(d => d.IssueKey).Order().SequenceEqual(a.Issues.Select(i => i.IssueKey).Order())
                        || pending.Any(d => !receipt.Decisions.Contains(d)))
                        throw new InvalidDataException("The server did not verify these exact quality findings and decisions.");
                    foreach (var decision in receipt.Decisions)
                    {
                        string id = LocalQualityDismissalStore.DecisionId(source, decision.IssueKey, current.ServerDocumentId, page.ServerPageId);
                        items.RemoveAll(d => Maps(d) && d.SourceHash == source && d.IssueKey == decision.IssueKey);
                        if (decision.Dismissed)
                        {
                            var issue = a.Issues.Single(i => i.IssueKey == decision.IssueKey);
                            items.Add(new(id, source, issue.IssueKey, issue.RuleId, issue.Message, issue.AnchorText is { Length: > 1000 } anchor ? anchor[..1000] : issue.AnchorText,
                                true, false, "Synced", current.ServerDocumentId, page.ServerPageId));
                        }
                    }
                    message = "Current full-page decisions verified with this account's backend. Other source versions remain local.";
                    if (receipt.LegacyIssueKeys?.Count > 0)
                        message += " Legacy client dismissals lack source evidence and remain separate; they were not imported into desktop decisions.";
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested && generation == account.Generation)
                { await Check(); Failed("Failed", "Synchronization timed out. Local decisions and pending restores are preserved; check again to retry."); }
                catch (Exception e) when (e is HttpRequestException or InvalidDataException or JsonException or DeviceSignInRequiredException or DeviceIdentityUnavailableException)
                {
                    await Check();
                    bool conflict = e is HttpRequestException { StatusCode: HttpStatusCode.Conflict };
                    Failed(conflict ? "Conflict" : "Failed", conflict
                        ? "Source mapping conflict. Decisions remain local; synchronize and check again. Changed passages are eligible for new findings."
                        : "Synchronization failed or is unsupported by this backend. Local decisions and pending restores are preserved; check again to retry.");
                }
                void Failed(string state, string explanation)
                {
                    message = explanation;
                    foreach (var d in eligible.Where(d => d.Pending)) items[items.IndexOf(d)] = d with { Status = state };
                }
            }
            await Check(); await store.SaveAsync(journal with { Items = items }, ct);
            var scoped = items.Where(Maps).ToArray();
            return new(scoped, scoped.Where(d => d.Dismissed && d.SourceHash == source && a.Issues.Any(i => i.IssueKey == d.IssueKey))
                .Select(d => d.IssueKey).ToHashSet(StringComparer.Ordinal), message);
        }
        finally { store.Gate.Release(); }
    }
}
