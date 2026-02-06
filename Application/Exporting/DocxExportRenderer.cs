using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WriterApp.Domain.Documents;
using WriterDocument = WriterApp.Domain.Documents.Document;

namespace WriterApp.Application.Exporting
{
    public sealed class DocxExportRenderer : IExportRenderer
    {
        public ExportFormat Format => ExportFormat.Docx;
        public ExportKind Kind => ExportKind.Document;

        public Task<ExportResult> RenderAsync(WriterDocument document, ExportOptions options)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            ExportOptions resolved = options ?? new ExportOptions();
            string title = ExportHelpers.GetDocumentTitle(document);

            using MemoryStream stream = new();
            using (WordprocessingDocument wordDoc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
            {
                MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(new Body());
                _ = EnsureNumberingDefinitions(mainPart);
                Body body = mainPart.Document.Body ?? new Body();
                mainPart.Document.Body = body;

                DocxHtmlConverter converter = new(body);

                bool breakOnH1 = ExportHelpers.HasChapterBreak(resolved, "h1");
                bool breakOnSection = ExportHelpers.HasChapterBreak(resolved, "section");
                bool hasContent = false;

                if (resolved.IncludeTitlePage)
                {
                    converter.AppendHeading(title, 1, pageBreakBefore: false);
                    converter.AppendPageBreak();
                }

                foreach (Section section in ExportHelpers.GetOrderedSections(document))
                {
                    if (breakOnSection && hasContent)
                    {
                        converter.AppendPageBreak();
                    }

                    string sectionTitle = ExportHelpers.GetSectionTitle(section);
                    converter.AppendHeading(sectionTitle, 2, pageBreakBefore: false);

                    string sectionHtml = ExportHelpers.BuildSectionHtml(section.Content, sectionTitle, allowStripHeading: true);
                    if (!string.IsNullOrWhiteSpace(sectionHtml))
                    {
                        converter.AppendHtml(sectionHtml, breakOnH1, ref hasContent);
                    }
                }

                mainPart.Document.Save();
            }

            byte[] payload = stream.ToArray();
            string fileName = ExportHelpers.SanitizeFileName(document.Metadata.Title, "document", ".docx");
            ExportResult result = new(
                payload,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                fileName);
            return Task.FromResult(result);
        }

        private static NumberingDefinitionsPart EnsureNumberingDefinitions(MainDocumentPart mainPart)
        {
            NumberingDefinitionsPart part = mainPart.NumberingDefinitionsPart ?? mainPart.AddNewPart<NumberingDefinitionsPart>();
            if (part.Numbering is not null)
            {
                return part;
            }

            AbstractNum bulletAbstract = new(new Level(
                    new StartNumberingValue { Val = 1 },
                    new NumberingFormat { Val = NumberFormatValues.Bullet },
                    new LevelText { Val = "•" },
                    new LevelJustification { Val = LevelJustificationValues.Left },
                    new ParagraphProperties(new Indentation { Left = "720", Hanging = "360" }))
                { LevelIndex = 0 })
            {
                AbstractNumberId = 1
            };
            bulletAbstract.AppendChild(new Level(
                    new StartNumberingValue { Val = 1 },
                    new NumberingFormat { Val = NumberFormatValues.Bullet },
                    new LevelText { Val = "o" },
                    new LevelJustification { Val = LevelJustificationValues.Left },
                    new ParagraphProperties(new Indentation { Left = "1440", Hanging = "360" }))
                { LevelIndex = 1 });

            AbstractNum orderedAbstract = new(new Level(
                    new StartNumberingValue { Val = 1 },
                    new NumberingFormat { Val = NumberFormatValues.Decimal },
                    new LevelText { Val = "%1." },
                    new LevelJustification { Val = LevelJustificationValues.Left },
                    new ParagraphProperties(new Indentation { Left = "720", Hanging = "360" }))
                { LevelIndex = 0 })
            {
                AbstractNumberId = 2
            };
            orderedAbstract.AppendChild(new Level(
                    new StartNumberingValue { Val = 1 },
                    new NumberingFormat { Val = NumberFormatValues.Decimal },
                    new LevelText { Val = "%2." },
                    new LevelJustification { Val = LevelJustificationValues.Left },
                    new ParagraphProperties(new Indentation { Left = "1440", Hanging = "360" }))
                { LevelIndex = 1 });

            Numbering numbering = new();
            numbering.Append(bulletAbstract);
            numbering.Append(orderedAbstract);
            numbering.Append(new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = 1 });
            numbering.Append(new NumberingInstance(new AbstractNumId { Val = 2 }) { NumberID = 2 });

            part.Numbering = numbering;
            part.Numbering.Save();
            return part;
        }

        private sealed class DocxHtmlConverter
        {
            private readonly Body _body;
            private readonly HtmlParser _parser = new();

            public DocxHtmlConverter(Body body)
            {
                _body = body ?? throw new ArgumentNullException(nameof(body));
            }

            public void AppendHeading(string text, int level, bool pageBreakBefore)
            {
                if (pageBreakBefore)
                {
                    AppendPageBreak();
                }

                Paragraph paragraph = new();
                ParagraphProperties properties = new(new ParagraphStyleId { Val = $"Heading{Math.Clamp(level, 1, 3)}" });
                paragraph.Append(properties);
                paragraph.Append(new Run(new Text(text ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve }));
                _body.Append(paragraph);
            }

            public void AppendPageBreak()
            {
                Paragraph breakParagraph = new();
                breakParagraph.Append(new Run(new Break { Type = BreakValues.Page }));
                _body.Append(breakParagraph);
            }

            public void AppendHtml(string html, bool breakOnH1, ref bool hasContent)
            {
                if (string.IsNullOrWhiteSpace(html))
                {
                    return;
                }

                IDocument document = _parser.ParseDocument($"<body>{html}</body>");
                IElement? body = document.Body;
                if (body is null)
                {
                    return;
                }

                AppendBlocks(body.ChildNodes, breakOnH1, ref hasContent);
            }

            private void AppendBlocks(IEnumerable<INode> nodes, bool breakOnH1, ref bool hasContent)
            {
                foreach (INode node in nodes)
                {
                    if (node is IElement element)
                    {
                        string tag = element.TagName.ToLowerInvariant();
                        if (tag is "p")
                        {
                            AppendParagraph(element.ChildNodes, null);
                            hasContent = true;
                            continue;
                        }

                        if (tag is "h1" or "h2" or "h3")
                        {
                            int level = tag == "h1" ? 1 : tag == "h2" ? 2 : 3;
                            if (breakOnH1 && level == 1 && hasContent)
                            {
                                AppendPageBreak();
                            }

                            AppendHeading(element.TextContent ?? string.Empty, level, pageBreakBefore: false);
                            hasContent = true;
                            continue;
                        }

                        if (tag is "ul" or "ol")
                        {
                            AppendList(element, tag == "ol", 0, breakOnH1, ref hasContent);
                            continue;
                        }

                        if (tag is "br")
                        {
                            Paragraph paragraph = new();
                            paragraph.Append(new Run(new Break()));
                            _body.Append(paragraph);
                            continue;
                        }

                        if (tag is "img")
                        {
                            // TODO: Support embedding images in DOCX exports.
                            AppendPlainParagraph("[Image omitted]");
                            hasContent = true;
                            continue;
                        }

                        AppendBlocks(element.ChildNodes, breakOnH1, ref hasContent);
                        continue;
                    }

                    if (node is IText textNode)
                    {
                        string text = textNode.Text;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            AppendParagraph(new[] { textNode }, null);
                            hasContent = true;
                        }
                    }
                }
            }

            private void AppendList(IElement list, bool ordered, int level, bool breakOnH1, ref bool hasContent)
            {
                int clampedLevel = Math.Clamp(level, 0, 1);
                foreach (IElement item in list.Children.Where(child => child.TagName.Equals("LI", StringComparison.OrdinalIgnoreCase)))
                {
                    AppendListItem(item, ordered, clampedLevel, breakOnH1, ref hasContent);
                }
            }

            private void AppendListItem(IElement item, bool ordered, int level, bool breakOnH1, ref bool hasContent)
            {
                IEnumerable<IElement> blockChildren = item.Children.Where(child =>
                    child.TagName.Equals("P", StringComparison.OrdinalIgnoreCase)
                    || child.TagName.Equals("H1", StringComparison.OrdinalIgnoreCase)
                    || child.TagName.Equals("H2", StringComparison.OrdinalIgnoreCase)
                    || child.TagName.Equals("H3", StringComparison.OrdinalIgnoreCase));

                bool wroteParagraph = false;
                foreach (IElement block in blockChildren)
                {
                    AppendListParagraph(block.ChildNodes, ordered, level);
                    wroteParagraph = true;
                    hasContent = true;
                }

                IEnumerable<INode> inlineNodes = item.ChildNodes.Where(node =>
                    node is not IElement el
                    || !el.TagName.Equals("UL", StringComparison.OrdinalIgnoreCase)
                    && !el.TagName.Equals("OL", StringComparison.OrdinalIgnoreCase));

                if (!wroteParagraph && inlineNodes.Any(node => node is IText text && !string.IsNullOrWhiteSpace(text.Text) || node is IElement))
                {
                    AppendListParagraph(inlineNodes, ordered, level);
                    hasContent = true;
                }

                foreach (IElement nested in item.Children.Where(child =>
                             child.TagName.Equals("UL", StringComparison.OrdinalIgnoreCase)
                             || child.TagName.Equals("OL", StringComparison.OrdinalIgnoreCase)))
                {
                    AppendList(nested, nested.TagName.Equals("OL", StringComparison.OrdinalIgnoreCase), level + 1, breakOnH1, ref hasContent);
                }
            }

            private void AppendListParagraph(IEnumerable<INode> inlineNodes, bool ordered, int level)
            {
                ParagraphProperties props = new();
                props.Append(new NumberingProperties(
                    new NumberingLevelReference { Val = level },
                    new NumberingId { Val = ordered ? 2 : 1 }));

                AppendParagraph(inlineNodes, props);
            }

            private void AppendParagraph(IEnumerable<INode> inlineNodes, ParagraphProperties? properties)
            {
                Paragraph paragraph = new();
                if (properties is not null)
                {
                    paragraph.Append(properties);
                }

                AppendInlineNodes(inlineNodes, paragraph, new InlineStyle());
                _body.Append(paragraph);
            }

            private void AppendPlainParagraph(string text)
            {
                Paragraph paragraph = new();
                paragraph.Append(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
                _body.Append(paragraph);
            }

            private void AppendInlineNodes(IEnumerable<INode> nodes, Paragraph paragraph, InlineStyle style)
            {
                foreach (INode node in nodes)
                {
                    if (node is IText textNode)
                    {
                        AppendRun(paragraph, textNode.Text, style);
                        continue;
                    }

                    if (node is not IElement element)
                    {
                        continue;
                    }

                    string tag = element.TagName.ToLowerInvariant();
                    InlineStyle nextStyle = style;
                    if (tag is "strong" or "b")
                    {
                        nextStyle = nextStyle with { Bold = true };
                    }
                    else if (tag is "em" or "i")
                    {
                        nextStyle = nextStyle with { Italic = true };
                    }
                    else if (tag is "u")
                    {
                        nextStyle = nextStyle with { Underline = true };
                    }
                    else if (tag is "br")
                    {
                        paragraph.Append(new Run(new Break()));
                        continue;
                    }
                    else if (tag is "img")
                    {
                        AppendRun(paragraph, "[Image omitted]", style);
                        continue;
                    }

                    AppendInlineNodes(element.ChildNodes, paragraph, nextStyle);
                }
            }

            private static void AppendRun(Paragraph paragraph, string? text, InlineStyle style)
            {
                if (paragraph is null)
                {
                    return;
                }

                string value = text ?? string.Empty;
                Run run = new();
                RunProperties props = new();
                if (style.Bold)
                {
                    props.Append(new Bold());
                }

                if (style.Italic)
                {
                    props.Append(new Italic());
                }

                if (style.Underline)
                {
                    props.Append(new Underline { Val = UnderlineValues.Single });
                }

                if (props.ChildElements.Count > 0)
                {
                    run.Append(props);
                }

                Text textNode = new(value) { Space = SpaceProcessingModeValues.Preserve };
                run.Append(textNode);
                paragraph.Append(run);
            }

            private readonly record struct InlineStyle(bool Bold = false, bool Italic = false, bool Underline = false);
        }
    }
}
