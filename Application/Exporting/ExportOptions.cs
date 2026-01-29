using System;

namespace WriterApp.Application.Exporting
{
    public sealed record ExportOptions(
        bool IncludeTitlePage = true,
        Guid? TemplateId = null,
        WriterApp.Data.Exporting.ExportTemplate? Template = null);
}
