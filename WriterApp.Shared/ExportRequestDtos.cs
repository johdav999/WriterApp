using System;
using System.Collections.Generic;

namespace WriterApp.Application.Exporting
{
    public sealed record ExportDocumentRequest(
        Guid DocumentId,
        string Format,
        Guid? TemplateId,
        string ScopeType,
        IReadOnlyList<Guid>? ScopeIds = null,
        SelectionRangeDto? SelectionRange = null,
        string? SelectionText = null);
}
