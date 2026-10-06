using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;
public sealed class LocalStyleQualityApprovalTests
{
    [Fact]
    public async Task PartialApprovalRetainsOriginalReviewAndExactApprovedRecoveryAcrossRestart()
    {
        string root = Path.Combine(Path.GetTempPath(), "WriterApp.StyleApproval", Guid.NewGuid().ToString("N"));
        try {
            const string source = "Elin walked very slowly. She was really tired.";
            var files = new FileLocalDocumentStore(root); var repository = new LocalDocumentRepository(files);
            var document = await repository.CreateImportedAsync("Draft", "<p>" + source + "</p>");
            document = await files.ApplySyncAsync(document with { ServerDocumentId = Guid.NewGuid(), ServerVersion = "v1", SyncState = LocalSyncState.Synced }, document.LocalRevision, default);
            var section = document.Sections[0]; var page = section.Pages[0];
            var snapshot = new AiEditorSnapshot(page.Content, source, source, 0, source.Length, 1, source.Length + 1, 0);
            var prepared = DeviceAiRequests.Build(document, section, page, snapshot, DeviceAiAction.StyleQuality);
            var report = new StyleQualityReport([
                new("Elin walked very slowly.", "Elin walked slowly.", "word_choice", "preference", "Remove an intensifier.", "Less emphasis on pace."),
                new("She was really tired.", "She was tired.", "clarity", "preference", "Remove emphasis.", "Less emphasis on fatigue.")]);
            var proposal = new DeviceAiProposal(prepared, StyleQualityReview.Compose(report, source, [0, 1]), null, source, new()) { StyleReview = report };
            var full = new LocalAiHistory(1, Guid.NewGuid(), document.DocumentId, prepared.Key, "Manuscript replace", document.LocalRevision,
                document.ServerVersion, DateTimeOffset.UtcNow, "Reviewed", document, proposal.ProposedText, page.PageId, OriginalText: source);
            string historyRoot = Path.Combine(root, "history"); var history = new LocalAiStore(historyRoot);
            await history.SaveHistoryAsync(full);
            var partial = proposal with { ProposedText = StyleQualityReview.Compose(report, source, [0]) };
            var approved = LocalStyleQualityApproval.Prepare(partial, [0], full);
            Assert.NotEqual(full.Id, approved.Id);
            Assert.Equal("Elin walked slowly. She was really tired.", approved.Proposed);
            Assert.Equal(full.Before, approved.Before);
            await history.SaveHistoryAsync(approved with { Status = "Applying" });
            string after = "<p>" + approved.Proposed + "</p>";
            var saved = await repository.SaveAsync(document with { Sections = [section with { Pages = [page with { Content = after }] }] });
            await history.SaveHistoryAsync(approved with { Status = "Applied", AfterHtml = after });
            var records = await new LocalAiStore(historyRoot).HistoryAsync(document.DocumentId);
            Assert.Equal("Reviewed", records.Single(h => h.Id == full.Id).Status);
            Assert.Equal(full.Proposed, records.Single(h => h.Id == full.Id).Proposed);
            var recovery = records.Single(h => h.Id == approved.Id);
            Assert.Equal("Applied", recovery.Status); Assert.Equal(partial.ProposedText, recovery.Proposed);
            Assert.Equal(page.Content, recovery.Before.Sections[0].Pages[0].Content);
            Assert.Equal(after, saved.Sections[0].Pages[0].Content);
            Assert.Throws<InvalidOperationException>(() => LocalStyleQualityApproval.Prepare(partial, [], full));
            Assert.Throws<InvalidDataException>(() => LocalStyleQualityApproval.Prepare(proposal, [0], full));
            Assert.Equal(full.Id, LocalStyleQualityApproval.Prepare(proposal, [0, 1], full).Id);
        } finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
