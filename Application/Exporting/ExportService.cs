using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Exporting
{
    public sealed class ExportService
    {
        private readonly IReadOnlyList<IExportRenderer> _renderers;

        public ExportService(IEnumerable<IExportRenderer> renderers)
        {
            _renderers = renderers?.ToList() ?? throw new ArgumentNullException(nameof(renderers));
        }

        public Task<ExportResult> ExportAsync(Document document, ExportFormat format, ExportOptions options)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            IExportRenderer renderer = _renderers.FirstOrDefault(candidate => candidate.Format == format)
                ?? throw new InvalidOperationException($"No export renderer registered for {format}.");

            return renderer.RenderAsync(document, options ?? new ExportOptions());
        }
    }
}
