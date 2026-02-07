using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
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

        [Fact]
        public void DocxExport_BulletsUseSymbolFontGlyph()
        {
            WriterDocument document = BuildDocument("<ul><li>Bullet</li></ul>");
            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                new ExportOptions(IncludeTitlePage: false),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
            NumberingDefinitionsPart? numberingPart = wordDoc.MainDocumentPart?.NumberingDefinitionsPart;
            Assert.NotNull(numberingPart);

            AbstractNum? bullets = numberingPart!.Numbering?.Elements<AbstractNum>()
                .FirstOrDefault(num => num.AbstractNumberId?.Value == 1);
            Assert.NotNull(bullets);

            Level? level0 = bullets!.Elements<Level>().FirstOrDefault(level => level.LevelIndex?.Value == 0);
            Assert.NotNull(level0);
            Assert.Equal(NumberFormatValues.Bullet, level0!.NumberingFormat?.Val?.Value);
            Assert.Equal("", level0.LevelText?.Val?.Value);
            Assert.Equal("Symbol", level0.NumberingSymbolRunProperties?.RunFonts?.Ascii);
            Assert.Equal("Symbol", level0.NumberingSymbolRunProperties?.RunFonts?.HighAnsi);
        }

        [Fact]
        public void DocxExport_HyperlinksCreateRelationships()
        {
            WriterDocument document = BuildDocument("<p>See <a href=\"https://example.com\"><strong>this</strong> link</a>.</p>");
            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                new ExportOptions(IncludeTitlePage: false),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
            var rel = wordDoc.MainDocumentPart?.HyperlinkRelationships.FirstOrDefault(r => string.Equals(r.Uri.Host, "example.com", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rel);

            var hyperlink = wordDoc.MainDocumentPart?.Document?.Body?.Descendants<Hyperlink>()
                .FirstOrDefault(link => link.Id == rel!.Id);
            Assert.NotNull(hyperlink);
        }

        [Fact]
        public void DocxExport_BrCreatesLineBreak()
        {
            WriterDocument document = BuildDocument("<p>Hello<br>World</p>");
            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                new ExportOptions(IncludeTitlePage: false),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
            var breaks = wordDoc.MainDocumentPart?.Document?.Body?.Descendants<Break>().ToList();
            Assert.NotNull(breaks);
            Assert.NotEmpty(breaks!);
        }

        [Fact]
        public void DocxExport_PreservesWhitespaceWhenNeeded()
        {
            WriterDocument document = BuildDocument("<p>Hello  world</p>");
            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                new ExportOptions(IncludeTitlePage: false),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
            var textNode = wordDoc.MainDocumentPart?.Document?.Body?.Descendants<Text>()
                .FirstOrDefault(t => t.Text.Contains("Hello", StringComparison.Ordinal));
            Assert.NotNull(textNode);
            Assert.Equal(SpaceProcessingModeValues.Preserve, textNode!.Space?.Value);
        }

        [Fact]
        public void DocxExport_SetsDocumentDefaults()
        {
            WriterDocument document = BuildDocument("<p>Defaults</p>");
            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                new ExportOptions(IncludeTitlePage: false),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);

            SectionProperties? sectionProps = wordDoc.MainDocumentPart?.Document?.Body?.Elements<SectionProperties>().FirstOrDefault();
            Assert.NotNull(sectionProps);
            Assert.NotNull(sectionProps!.GetFirstChild<PageMargin>());

            StyleDefinitionsPart? stylesPart = wordDoc.MainDocumentPart?.StyleDefinitionsPart;
            Assert.NotNull(stylesPart);
            Style? normal = stylesPart!.Styles?.Elements<Style>()
                .FirstOrDefault(style => string.Equals(style.StyleId?.Value, "Normal", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(normal);
            Assert.NotNull(normal!.StyleRunProperties?.RunFonts);
            Assert.NotNull(normal.StyleRunProperties?.FontSize);
            Assert.NotNull(normal.StyleParagraphProperties?.SpacingBetweenLines);
        }

        [Fact]
        public void DocxExport_IncludesTocField()
        {
            WriterDocument document = BuildDocument("<h1>Chapter One</h1><p>Body</p>");
            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                new ExportOptions(IncludeTitlePage: false, IncludeToc: true),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
            var tocField = wordDoc.MainDocumentPart?.Document?.Body?
                .Descendants<SimpleField>()
                .FirstOrDefault(field => (field.Instruction?.Value ?? string.Empty)
                    .Contains("TOC", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(tocField);
        }

        [Fact]
        public void DocxExport_AddsHeaderFooterWithPageNumber()
        {
            WriterDocument document = BuildDocument("<p>Body</p>");
            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                new ExportOptions(IncludeTitlePage: false),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);

            Assert.NotNull(wordDoc.MainDocumentPart?.HeaderParts.FirstOrDefault());
            Assert.NotNull(wordDoc.MainDocumentPart?.FooterParts.FirstOrDefault());

            SectionProperties? sectionProps = wordDoc.MainDocumentPart?.Document?.Body?.Elements<SectionProperties>().FirstOrDefault();
            Assert.NotNull(sectionProps);
            Assert.NotNull(sectionProps!.GetFirstChild<HeaderReference>());
            Assert.NotNull(sectionProps.GetFirstChild<FooterReference>());

            FooterPart? footerPart = wordDoc.MainDocumentPart?.FooterParts.FirstOrDefault();
            var pageField = footerPart?.Footer?.Descendants<SimpleField>()
                .FirstOrDefault(field => (field.Instruction?.Value ?? string.Empty)
                    .Contains("PAGE", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(pageField);
        }

        [Fact]
        public void DocxExport_TablesRenderRowsAndCells()
        {
            WriterDocument document = BuildDocument("<table><tr><td>A</td><td>B</td></tr><tr><td>C</td><td>D</td></tr></table>");
            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                new ExportOptions(IncludeTitlePage: false),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
            Table? table = wordDoc.MainDocumentPart?.Document?.Body?.Elements<Table>().FirstOrDefault();
            Assert.NotNull(table);
            Assert.Equal(2, table!.Elements<TableRow>().Count());
            Assert.All(table.Elements<TableRow>(), row => Assert.Equal(2, row.Elements<TableCell>().Count()));
        }

        [Fact]
        public void DocxExport_TableHeaderAndRichCellContentArePreserved()
        {
            string html = """
                          <table>
                            <thead>
                              <tr>
                                <th>Chapter</th>
                                <th>Notes</th>
                              </tr>
                            </thead>
                            <tbody>
                              <tr>
                                <td>
                                  <p>Line one.</p>
                                  <p>Line two with <strong>bold</strong> and <em>italic</em>.</p>
                                  <ul><li>Point A</li><li>Point B</li></ul>
                                </td>
                                <td>Cell 2</td>
                              </tr>
                            </tbody>
                          </table>
                          """;

            WriterDocument document = BuildDocument(html);
            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                new ExportOptions(IncludeTitlePage: false),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
            Table? table = wordDoc.MainDocumentPart?.Document?.Body?.Elements<Table>().FirstOrDefault();
            Assert.NotNull(table);
            Assert.Equal(2, table!.Elements<TableRow>().Count());
            Assert.Equal(4, table.Descendants<TableCell>().Count());
            Assert.Contains("Chapter", table.InnerText, StringComparison.Ordinal);
            Assert.Contains("Line one.", table.InnerText, StringComparison.Ordinal);
            Assert.Contains("Point A", table.InnerText, StringComparison.Ordinal);
        }

        [Fact]
        public void DocxExport_BlockquoteAddsIndent()
        {
            WriterDocument document = BuildDocument("<blockquote>Quote</blockquote>");
            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                new ExportOptions(IncludeTitlePage: false),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
            Paragraph? paragraph = wordDoc.MainDocumentPart?.Document?.Body?.Elements<Paragraph>()
                .FirstOrDefault(p => p.InnerText.Contains("Quote", StringComparison.Ordinal));
            Assert.NotNull(paragraph);
            Assert.NotNull(paragraph!.ParagraphProperties?.Indentation?.Left);
        }

        [Fact]
        public void DocxExport_CodeBlockUsesMonospaceAndBreaks()
        {
            WriterDocument document = BuildDocument("<pre><code>line1\nline2</code></pre>");
            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                new ExportOptions(IncludeTitlePage: false),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
            Paragraph? paragraph = wordDoc.MainDocumentPart?.Document?.Body?.Elements<Paragraph>()
                .FirstOrDefault(p => p.InnerText.Contains("line1", StringComparison.Ordinal));
            Assert.NotNull(paragraph);
            Assert.NotEmpty(paragraph!.Descendants<Break>());

            RunFonts? fonts = paragraph.Descendants<RunFonts>().FirstOrDefault();
            Assert.NotNull(fonts);
            Assert.Equal("Consolas", fonts!.Ascii);
        }

        [Fact]
        public void DocxExport_DataUriImageCreatesImagePart()
        {
            string pngBase64 =
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO0nJ9sAAAAASUVORK5CYII=";
            string html = $"<p>Image <img src=\"data:image/png;base64,{pngBase64}\" /></p>";
            WriterDocument document = BuildDocument(html);
            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                new ExportOptions(IncludeTitlePage: false),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
            Assert.NotEmpty(wordDoc.MainDocumentPart!.ImageParts);
            Assert.NotEmpty(wordDoc.MainDocumentPart!.Document!.Body!.Descendants<Drawing>());
        }

        [Fact]
        public void DocxExport_DataUriImageKeepsParagraphOrder()
        {
            string pngBase64 =
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO0nJ9sAAAAASUVORK5CYII=";
            string html = $"<p>Before image</p><p><img src=\"data:image/png;base64,{pngBase64}\" alt=\"Sample\" width=\"320\" /></p><p>After image</p>";
            WriterDocument document = BuildDocument(html);
            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                new ExportOptions(IncludeTitlePage: false),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
            var paragraphs = wordDoc.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ToList();
            Assert.True(paragraphs.Any(p => p.InnerText.Contains("Before image", StringComparison.Ordinal)));
            Assert.True(paragraphs.Any(p => p.InnerText.Contains("After image", StringComparison.Ordinal)));
            Assert.NotEmpty(wordDoc.MainDocumentPart!.Document!.Body!.Descendants<Drawing>());
        }

        [Fact]
        public void DocxExport_RemoteImageDisabledFallsBackToPlaceholder()
        {
            WriterDocument document = BuildDocument("<p><img src=\"https://example.com/image.png\" /></p>");
            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                new ExportOptions(IncludeTitlePage: false),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
            Assert.Empty(wordDoc.MainDocumentPart!.ImageParts);
            Assert.Contains("Image omitted", wordDoc.MainDocumentPart!.Document!.Body!.InnerText, StringComparison.Ordinal);
        }

        [Fact]
        public void DocxExport_OrderedListsUseSeparateNumberingInstances()
        {
            WriterDocument document = BuildDocument(
                "<ol><li>First list item</li></ol>" +
                "<p>Gap</p>" +
                "<ol><li>Second list item</li></ol>");

            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                new ExportOptions(IncludeTitlePage: false),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
            var paragraphs = wordDoc.MainDocumentPart?.Document?.Body?.Elements<Paragraph>().ToList() ?? throw new InvalidOperationException("No document body.");

            Paragraph? first = paragraphs.FirstOrDefault(p => p.InnerText.Contains("First list item", StringComparison.Ordinal));
            Paragraph? second = paragraphs.FirstOrDefault(p => p.InnerText.Contains("Second list item", StringComparison.Ordinal));
            Assert.NotNull(first);
            Assert.NotNull(second);

            int? firstNumId = first!.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
            int? secondNumId = second!.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
            Assert.NotNull(firstNumId);
            Assert.NotNull(secondNumId);
            Assert.NotEqual(firstNumId, secondNumId);
        }

        [Fact]
        public void DocxExport_NestedListsUseIncrementedLevels()
        {
            WriterDocument document = BuildDocument("<ul><li>Outer<ul><li>Inner</li></ul></li></ul>");

            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                new ExportOptions(IncludeTitlePage: false),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
            var paragraphs = wordDoc.MainDocumentPart?.Document?.Body?.Elements<Paragraph>().ToList() ?? throw new InvalidOperationException("No document body.");

            Paragraph? outer = paragraphs.FirstOrDefault(p => p.InnerText.Contains("Outer", StringComparison.Ordinal));
            Paragraph? inner = paragraphs.FirstOrDefault(p => p.InnerText.Contains("Inner", StringComparison.Ordinal));
            Assert.NotNull(outer);
            Assert.NotNull(inner);

            int? outerLevel = outer!.ParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value;
            int? innerLevel = inner!.ParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value;
            Assert.Equal(0, outerLevel);
            Assert.Equal(1, innerLevel);
        }

        [Fact]
        public void DocxExport_ClampsListDepthToEight()
        {
            string html = string.Concat(Enumerable.Repeat("<ul><li>", 10))
                + "Deep"
                + string.Concat(Enumerable.Repeat("</li></ul>", 10));

            WriterDocument document = BuildDocument(html);
            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Docx,
                new ExportOptions(IncludeTitlePage: false),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
            var levels = wordDoc.MainDocumentPart?.Document?.Body?
                .Elements<Paragraph>()
                .Select(p => p.ParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value)
                .Where(level => level.HasValue)
                .Select(level => level!.Value)
                .ToList() ?? throw new InvalidOperationException("No document body.");

            Assert.True(levels.Count > 0);
            Assert.True(levels.Max() <= 8);
        }

        private static ExportService BuildExportService()
        {
            IExportRenderer[] renderers =
            {
                new DocxExportRenderer(
                    NullLogger<DocxExportRenderer>.Instance,
                    BuildConfig(("Exports:DocxFetchRemoteImages", "false")),
                    new StubHttpClientFactory())
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

        private static IConfiguration BuildConfig(params (string Key, string Value)[] pairs)
        {
            Dictionary<string, string?> values = pairs.ToDictionary(pair => pair.Key, pair => (string?)pair.Value);
            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }

        private sealed class StubHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new();
        }
    }
}
