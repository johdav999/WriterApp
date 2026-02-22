using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.Data.Exporting;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Exporting
{
    public sealed class ExportService
    {
        private readonly IReadOnlyList<IExportRenderer> _renderers;
        private readonly IExportTemplateResolver _templateResolver;

        public ExportService(IEnumerable<IExportRenderer> renderers, IExportTemplateResolver templateResolver)
        {
            _renderers = renderers?.ToList() ?? throw new ArgumentNullException(nameof(renderers));
            _templateResolver = templateResolver ?? throw new ArgumentNullException(nameof(templateResolver));
        }

        public async Task<ExportResult> ExportAsync(
            Document document,
            ExportKind kind,
            ExportFormat format,
            ExportOptions options,
            string ownerUserId,
            Guid? templateId,
            CancellationToken ct)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            IExportRenderer renderer = _renderers.FirstOrDefault(candidate => candidate.Format == format && candidate.Kind == kind)
                ?? throw new InvalidOperationException($"No export renderer registered for {kind} {format}.");

            ExportOptions resolved = options ?? new ExportOptions();
            resolved = await ApplyTemplateAsync(resolved, kind, format, ownerUserId, templateId, ct);
            return await renderer.RenderAsync(document, resolved);
        }


        public async Task<string> ExportHtmlBodyAsync(
            Document document,
            ExportKind kind,
            ExportOptions options,
            string ownerUserId,
            Guid? templateId,
            CancellationToken ct)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            TemplatedHtmlExportRenderer? renderer = _renderers
                .OfType<TemplatedHtmlExportRenderer>()
                .FirstOrDefault(candidate => candidate.Kind == kind);
            if (renderer is null)
            {
                throw new InvalidOperationException($"No HTML export renderer registered for {kind}.");
            }

            ExportOptions resolved = options ?? new ExportOptions();
            resolved = await ApplyTemplateAsync(resolved, kind, ExportFormat.Html, ownerUserId, templateId, ct);
            return renderer.RenderBodyHtml(document, resolved);
        }

        private async Task<ExportOptions> ApplyTemplateAsync(
            ExportOptions options,
            ExportKind kind,
            ExportFormat format,
            string ownerUserId,
            Guid? templateId,
            CancellationToken ct)
        {
            if (kind != ExportKind.Document || format != ExportFormat.Html)
            {
                return options;
            }

            if (options.Template is not null)
            {
                return options;
            }

            ExportTemplate template = await _templateResolver.ResolveAsync(ownerUserId, templateId, ct);
            return options with
            {
                TemplateId = template.Id,
                Template = template
            };
        }
    }
}
