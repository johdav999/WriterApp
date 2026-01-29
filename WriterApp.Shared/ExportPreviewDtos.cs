using System;

namespace WriterApp.Application.Exporting
{
    public sealed record ExportPreviewRequest(
        Guid DocumentId,
        Guid? TemplateId,
        bool IncludeToc,
        string Scope,
        Guid? SectionId = null);

    public sealed record ExportPreviewResponse(string Html);
}
