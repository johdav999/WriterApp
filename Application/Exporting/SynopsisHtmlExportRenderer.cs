using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WriterApp.Domain.Documents;
using SynopsisModel = WriterApp.Domain.Documents.Synopsis;

namespace WriterApp.Application.Exporting
{
    public sealed class SynopsisHtmlExportRenderer : IExportRenderer
    {
        public ExportFormat Format => ExportFormat.Html;
        public ExportKind Kind => ExportKind.Synopsis;

        public Task<ExportResult> RenderAsync(Document document, ExportOptions options)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string body = RenderBodyHtml(document);
            string title = ExportHelpers.GetDocumentTitle(document);
            StringBuilder builder = new();
            builder.Append("<!DOCTYPE html>\n")
                .Append("<html>\n")
                .Append("<head>\n")
                .Append("  <meta charset=\"utf-8\" />\n")
                .Append("  <title>").Append(WebUtility.HtmlEncode(title)).Append(" - Synopsis</title>\n")
                .Append("  <style>\n")
                .Append("    body { max-width: 700px; margin: 3rem auto; font-family: serif; }\n")
                .Append("    h1 { margin-top: 2rem; }\n")
                .Append("    p { line-height: 1.6; }\n")
                .Append("  </style>\n")
                .Append("</head>\n")
                .Append("<body>\n")
                .Append(body)
                .Append("</body>\n</html>\n");

            string html = ExportHelpers.NormalizeLineEndings(builder.ToString());
            byte[] content = Encoding.UTF8.GetBytes(html);
            string fileName = ExportHelpers.SanitizeFileName(document.Metadata.Title, "synopsis", ".html");
            ExportResult result = new(content, "text/html", fileName);
            return Task.FromResult(result);
        }

        private static string RenderBodyHtml(Document document)
        {
            SynopsisModel synopsis = document.Synopsis ?? new SynopsisModel();
            StringBuilder builder = new();

            foreach (SynopsisExportHelpers.SynopsisEntry entry in SynopsisExportHelpers.GetOrderedEntries(synopsis))
            {
                builder.Append("  <section>\n");
                builder.Append("    <h1>").Append(WebUtility.HtmlEncode(entry.Label)).Append("</h1>\n");
                if (!string.IsNullOrWhiteSpace(entry.Value))
                {
                    builder.Append("    <p>").Append(WebUtility.HtmlEncode(entry.Value.Trim())).Append("</p>\n");
                }
                builder.Append("  </section>\n");
            }

            return builder.ToString();
        }
    }
}
