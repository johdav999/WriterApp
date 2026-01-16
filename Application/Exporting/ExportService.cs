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
<<<<<<< HEAD

        public Task<string> ExportHtmlBodyAsync(Document document, ExportOptions options)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            HtmlExportRenderer? renderer = _renderers.OfType<HtmlExportRenderer>().FirstOrDefault();
            if (renderer is null)
            {
                throw new InvalidOperationException("No HTML export renderer registered.");
            }

            return Task.FromResult(renderer.RenderBodyHtml(document, options ?? new ExportOptions()));
        }
=======
>>>>>>> ebb7526 (Implemented export of md and html)
    }
}
