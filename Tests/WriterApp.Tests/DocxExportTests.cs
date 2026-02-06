using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WriterApp.Application.Exporting;
using WriterApp.Domain.Documents;
using WriterDocument = WriterApp.Domain.Documents.Document;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class DocxExportTests
    {
        [Fact]
        public void DocxExport_IncludesHeadingsMarksListsAndPageBreaks()
        {
            WriterDocument document = BuildDocument(
                "<h1>Chapter One</h1>" +
                "<p>This is <strong>bold</strong>, <em>italic</em>, and <u>underline</u>.</p>" +
                "<ul><li>Item one</li><li>Item two</li></ul>");

            ExportService service = BuildExportService();
            ExportOptions options = new(IncludeTitlePage: false, ChapterBreakRules: new[] { "h1" });
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                options,
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
            var paragraphs = wordDoc.MainDocumentPart?.Document?.Body?.Elements<Paragraph>().ToList() ?? throw new InvalidOperationException("No document body.");

            Assert.Contains(paragraphs, p => p.ParagraphProperties?.ParagraphStyleId?.Val == "Heading1");
            Assert.Contains(paragraphs, p => p.ParagraphProperties?.ParagraphStyleId?.Val == "Heading2");

            var runs = paragraphs.SelectMany(p => p.Elements<Run>()).ToList();
            Assert.Contains(runs, r => r.RunProperties?.Bold is not null);
            Assert.Contains(runs, r => r.RunProperties?.Italic is not null);
            Assert.Contains(runs, r => r.RunProperties?.Underline is not null);

            Assert.Contains(paragraphs, p => p.ParagraphProperties?.NumberingProperties is not null);

            bool hasPageBreak = paragraphs.SelectMany(p => p.Descendants<Break>())
                .Any(b => b.Type?.Value == BreakValues.Page);
            Assert.True(hasPageBreak);
        }

        private static ExportService BuildExportService()
        {
            IExportRenderer[] renderers =
            {
                new DocxExportRenderer()
            };
            return new ExportService(renderers, new StubExportTemplateResolver());
        }
        private static WriterDocument BuildDocument(string html)
        {
            return new WriterDocument
            {
                DocumentId = Guid.NewGuid(),
                Metadata = new DocumentMetadata
                {
                    Title = "Docx Sample",
                    Language = "en",
                    Author = "Writer"
                },
                Synopsis = new Synopsis { ModifiedUtc = DateTime.UtcNow },
                Chapters =
                {
                    new Chapter
                    {
                        Order = 0,
                        Title = "Docx Sample",
                        Sections =
                        {
                            new Section
                            {
                                SectionId = Guid.NewGuid(),
                                Order = 0,
                                Title = "Section One",
                                Content = new SectionContent
                                {
                                    Format = "html",
                                    Value = html
                                },
                                Notes = string.Empty,
                                AI = new SectionAIInfo()
                            }
                        }
                    }
                }
            };
        }

        private sealed class StubExportTemplateResolver : IExportTemplateResolver
        {
            public Task<WriterApp.Data.Exporting.ExportTemplate> ResolveAsync(string ownerUserId, Guid? templateId, CancellationToken ct)
            {
                throw new InvalidOperationException("Templates are not used for DOCX exports.");
            }
        }
    }
}
