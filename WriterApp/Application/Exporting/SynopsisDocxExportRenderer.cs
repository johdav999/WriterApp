using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WriterApp.Domain.Documents;
using SynopsisModel = WriterApp.Domain.Documents.Synopsis;
using WriterDocument = WriterApp.Domain.Documents.Document;

namespace WriterApp.Application.Exporting
{
    public sealed class SynopsisDocxExportRenderer : IExportRenderer
    {
        private static readonly Regex ParagraphSplitRegex = new(@"\n\s*\n", RegexOptions.Compiled);

        public ExportFormat Format => ExportFormat.Docx;
        public ExportKind Kind => ExportKind.Synopsis;

        public Task<ExportResult> RenderAsync(WriterDocument document, ExportOptions options)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string title = ExportHelpers.GetDocumentTitle(document);
            SynopsisModel synopsis = document.Synopsis ?? new SynopsisModel();

            using MemoryStream stream = new();
            using (WordprocessingDocument wordDoc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
            {
                MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(new Body());
                EnsureStyles(mainPart);

                Body body = mainPart.Document.Body ?? new Body();
                mainPart.Document.Body = body;

                AppendHeading(body, title, 1);
                AppendHeading(body, "Synopsis", 2);

                foreach (string paragraph in GetSynopsisParagraphs(synopsis))
                {
                    AppendParagraph(body, paragraph);
                }

                mainPart.Document.Save();
            }

            byte[] payload = stream.ToArray();
            string fileName = ExportHelpers.SanitizeFileName($"{title}-Synopsis", "Synopsis", ".docx");
            return Task.FromResult(new ExportResult(
                payload,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                fileName));
        }

        private static IEnumerable<string> GetSynopsisParagraphs(SynopsisModel synopsis)
        {
            foreach (SynopsisExportHelpers.SynopsisEntry entry in SynopsisExportHelpers.GetOrderedEntries(synopsis))
            {
                if (string.IsNullOrWhiteSpace(entry.Value))
                {
                    continue;
                }

                string normalized = ExportHelpers.NormalizeLineEndings(entry.Value.Trim());
                string[] paragraphs = ParagraphSplitRegex.Split(normalized);
                foreach (string paragraph in paragraphs)
                {
                    string text = paragraph.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        yield return text;
                    }
                }
            }
        }

        private static void AppendHeading(Body body, string text, int level)
        {
            ParagraphProperties properties = new(new ParagraphStyleId { Val = level <= 1 ? "Heading1" : "Heading2" });
            Paragraph paragraph = new(properties, new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
            body.Append(paragraph);
        }

        private static void AppendParagraph(Body body, string value)
        {
            Paragraph paragraph = new();
            string[] lines = ExportHelpers.NormalizeLineEndings(value).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    paragraph.Append(new Run(new Break()));
                }

                paragraph.Append(new Run(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve }));
            }

            body.Append(paragraph);
        }

        private static void EnsureStyles(MainDocumentPart mainPart)
        {
            StyleDefinitionsPart stylePart = mainPart.StyleDefinitionsPart ?? mainPart.AddNewPart<StyleDefinitionsPart>();
            Styles styles = stylePart.Styles ?? new Styles();

            EnsureParagraphStyle(styles, "Normal", "Normal", "22", isDefault: true);
            EnsureParagraphStyle(styles, "Heading1", "Heading 1", "32");
            EnsureParagraphStyle(styles, "Heading2", "Heading 2", "28");

            stylePart.Styles = styles;
            stylePart.Styles.Save();
        }

        private static void EnsureParagraphStyle(Styles styles, string styleId, string styleName, string fontSize, bool isDefault = false)
        {
            Style? style = null;
            foreach (Style candidate in styles.Elements<Style>())
            {
                if (string.Equals(candidate.StyleId?.Value, styleId, StringComparison.OrdinalIgnoreCase))
                {
                    style = candidate;
                    break;
                }
            }

            if (style is null)
            {
                style = new Style
                {
                    Type = StyleValues.Paragraph,
                    StyleId = styleId
                };
                if (isDefault)
                {
                    style.Default = true;
                }

                style.Append(new StyleName { Val = styleName });
                styles.Append(style);
            }

            style.StyleRunProperties = new StyleRunProperties(
                new RunFonts
                {
                    Ascii = "Calibri",
                    HighAnsi = "Calibri",
                    ComplexScript = "Calibri"
                },
                new FontSize { Val = fontSize },
                new FontSizeComplexScript { Val = fontSize });

            style.StyleParagraphProperties = new StyleParagraphProperties(
                new SpacingBetweenLines
                {
                    After = "160",
                    Line = "276",
                    LineRule = LineSpacingRuleValues.Auto
                });
        }
    }
}
