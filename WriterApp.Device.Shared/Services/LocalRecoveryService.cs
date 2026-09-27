using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

public sealed class LocalRecoveryService(LocalRecoveryStore recovery, LocalDocumentRepository documents)
{
    public async Task<LocalRecoveryList> DiscoverAsync()
    {
        LocalRecoveryList found = await recovery.ListAsync();
        var candidates = new List<LocalRecoveryRecord>();
        foreach (LocalRecoveryRecord record in found.Records)
        {
            LocalDocument? current;
            try { current = await documents.LoadAsync(record.Document.DocumentId); }
            catch (LocalDocumentReadException) { current = null; }
            // Discovery is read-only: another process might replace this journal after our read.
            // Only its owning coordinator or an explicit recovery decision removes a record.
            if (current is null || !LocalRecoveryStore.Matches(current, record.Document))
                candidates.Add(record); // A stale-base conflict must preserve the unsaved writing.
        }
        return new(candidates, found.Issues);
    }

    public Task DiscardAsync(Guid id) => recovery.DiscardAsync(id);

    public async Task<LocalDocument> RestoreAsync(LocalRecoveryRecord record)
    {
        LocalDocument copy = await documents.CreateAsync((record.Document.Title.Length > 170
            ? record.Document.Title[..170] : record.Document.Title) + " (recovered)");
        copy = await documents.SaveAsync(copy with { Sections = record.Document.Sections.Select(s => s with
            { SectionId = Guid.NewGuid(), ServerSectionId = null, Pages = s.Pages.Select(p => p with
                { PageId = Guid.NewGuid(), ServerPageId = null }).ToArray() }).ToArray(), Kind = record.Document.Kind,
            LanguageCode = record.Document.LanguageCode });
        await recovery.DiscardAsync(record.RecoveryId);
        return copy;
    }
}
