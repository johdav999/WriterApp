using System;
using System.Text;
using System.Threading.Tasks;
using WriterApp.Domain.Documents;
using SynopsisModel = WriterApp.Domain.Documents.Synopsis;

namespace WriterApp.Application.Exporting
{
    public sealed class SynopsisMarkdownExportRenderer : IExportRenderer
    {
        public ExportFormat Format => ExportFormat.Markdown;
        public ExportKind Kind => ExportKind.Synopsis;

        public Task<ExportResult> RenderAsync(Document document, ExportOptions options)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            SynopsisModel synopsis = document.Synopsis ?? new SynopsisModel();
            StringBuilder builder = new();

            foreach (SynopsisExportHelpers.SynopsisEntry entry in SynopsisExportHelpers.GetOrderedEntries(synopsis))
            {
                builder.Append("# ").Append(entry.Label).Append("\n\n");
                if (!string.IsNullOrWhiteSpace(entry.Value))
                {
                    builder.Append(entry.Value.Trim()).Append("\n\n");
                }
                else
                {
                    builder.Append("\n");
                }
            }

            string markdown = ExportHelpers.NormalizeLineEndings(builder.ToString()).TrimEnd() + "\n";
            byte[] content = Encoding.UTF8.GetBytes(markdown);
            string fileName = ExportHelpers.SanitizeFileName(document.Metadata.Title, "synopsis", ".md");

            ExportResult result = new(content, "text/markdown", fileName);
            return Task.FromResult(result);
        }
    }
}
