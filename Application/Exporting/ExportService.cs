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

        public Task<ExportResult> ExportAsync(Document document, ExportKind kind, ExportFormat format, ExportOptions options)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            IExportRenderer renderer = _renderers.FirstOrDefault(candidate => candidate.Format == format && candidate.Kind == kind)
                ?? throw new InvalidOperationException($"No export renderer registered for {kind} {format}.");

            return renderer.RenderAsync(document, options ?? new ExportOptions());
        }


        public Task<string> ExportHtmlBodyAsync(Document document, ExportKind kind, ExportOptions options)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            HtmlExportRenderer? renderer = _renderers
                .OfType<HtmlExportRenderer>()
                .FirstOrDefault(candidate => candidate.Kind == kind);
            if (renderer is null)
            {
                throw new InvalidOperationException($"No HTML export renderer registered for {kind}.");
            }

            return Task.FromResult(renderer.RenderBodyHtml(document, options ?? new ExportOptions()));
        }
    }
}
