using System.Text;
using WriterApp.Application.Exporting;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class ExportTests
    {
        private static ExportService BuildExportService()
        {
            return new ExportService(new IExportRenderer[]
            {
                new MarkdownExportRenderer(),
                new HtmlExportRenderer(),
                new SynopsisMarkdownExportRenderer(),
                new SynopsisHtmlExportRenderer()
            });
        }

        [Fact]
        public void DocumentExport_DoesNotIncludeSynopsis()
        {
            Document document = DocumentFactory.CreateNewDocument();
            document.Synopsis.Premise = "SYNOPSIS_ONLY_TEXT";
            ReplaceFirstSectionContent(document, "Body text.");

            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(document, ExportKind.Document, ExportFormat.Markdown, new ExportOptions()).GetAwaiter().GetResult();

            string output = Encoding.UTF8.GetString(result.Content);
            Assert.DoesNotContain("SYNOPSIS_ONLY_TEXT", output);
        }

        [Fact]
        public void SynopsisExport_DoesNotIncludeDocumentContent()
        {
            Document document = DocumentFactory.CreateNewDocument();
            document.Synopsis.Premise = "Synopsis premise";
            ReplaceFirstSectionContent(document, "DOC_ONLY_TEXT");

            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(document, ExportKind.Synopsis, ExportFormat.Markdown, new ExportOptions()).GetAwaiter().GetResult();

            string output = Encoding.UTF8.GetString(result.Content);
            Assert.DoesNotContain("DOC_ONLY_TEXT", output);
            Assert.Contains("Synopsis premise", output);
        }

        [Fact]
        public void DocumentExport_Html_DoesNotIncludeSynopsis()
        {
            Document document = DocumentFactory.CreateNewDocument();
            document.Synopsis.Premise = "SYNOPSIS_ONLY_TEXT";
            ReplaceFirstSectionContent(document, "Body text.");

            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(document, ExportKind.Document, ExportFormat.Html, new ExportOptions()).GetAwaiter().GetResult();

            string output = Encoding.UTF8.GetString(result.Content);
            Assert.DoesNotContain("SYNOPSIS_ONLY_TEXT", output);
        }

        [Fact]
        public void SynopsisExport_Html_DoesNotIncludeDocumentContent()
        {
            Document document = DocumentFactory.CreateNewDocument();
            document.Synopsis.Premise = "Synopsis premise";
            ReplaceFirstSectionContent(document, "DOC_ONLY_TEXT");

            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(document, ExportKind.Synopsis, ExportFormat.Html, new ExportOptions()).GetAwaiter().GetResult();

            string output = Encoding.UTF8.GetString(result.Content);
            Assert.DoesNotContain("DOC_ONLY_TEXT", output);
            Assert.Contains("Synopsis premise", output);
        }

        private static void ReplaceFirstSectionContent(Document document, string value)
        {
            Section section = document.Chapters[0].Sections[0];
            document.Chapters[0].Sections[0] = section with
            {
                Content = new SectionContent
                {
                    Format = "markdown",
                    Value = value
                }
            };
        }
    }
}
