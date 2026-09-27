using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

public sealed class DeviceDocumentTransfer(LocalDocumentRepository documents, IDeviceFileDialog dialog)
{
    public bool IsAvailable => dialog.IsAvailable;

    public async Task<DevicePreparedImport?> PickImportAsync(CancellationToken ct = default)
    {
        DeviceImportFile? file = await dialog.PickImportAsync(ct);
        return file is null ? null : DeviceDocumentFormats.ParseImport(file);
    }

    public async Task<LocalDocument> ImportPageAsync(LocalDocument document, Guid pageId,
        DevicePreparedImport import, DeviceImportMode mode, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(import);
        if (document.DeletedAtUtc is not null) throw new InvalidOperationException("Restore the document before importing.");
        LocalSection section = document.Sections.Single(s => s.Pages.Any(p => p.PageId == pageId));
        LocalPage page = section.Pages.Single(p => p.PageId == pageId);
        string merged = DeviceDocumentFormats.MergePage(page, import.Html, mode);
        LocalDocument updated = document with
        {
            Sections = document.Sections.Select(s => s.SectionId != section.SectionId ? s : s with
            {
                Pages = s.Pages.Select(p => p.PageId == pageId
                    ? p with { Content = merged, ContentFormat = LocalContentFormat.Html } : p).ToArray()
            }).ToArray()
        };
        return await documents.SaveAsync(updated, ct);
    }

    public async Task<bool> ExportAsync(LocalDocument document, DeviceExportFormat format, CancellationToken ct = default)
    {
        DevicePreparedExport export = DeviceDocumentFormats.Export(document, format);
        return await dialog.SaveAsync(export.SuggestedFileName, export.Extension, export.Content, ct);
    }
}
