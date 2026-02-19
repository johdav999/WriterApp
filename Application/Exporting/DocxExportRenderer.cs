using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WriterApp.Domain.Documents;
using WriterDocument = WriterApp.Domain.Documents.Document;
using A = DocumentFormat.OpenXml.Drawing;
using Wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using Pic = DocumentFormat.OpenXml.Drawing.Pictures;

namespace WriterApp.Application.Exporting
{
    public sealed class DocxExportRenderer : IExportRenderer
    {
        private readonly ILogger<DocxExportRenderer> _logger;
        private readonly bool _fetchRemoteImages;
        private readonly IHttpClientFactory _httpClientFactory;

        public DocxExportRenderer(
            ILogger<DocxExportRenderer> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fetchRemoteImages = configuration?.GetValue<bool?>("Exports:DocxFetchRemoteImages") ?? false;
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

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
                EnsureStyleDefaults(mainPart);
                Body body = mainPart.Document.Body ?? new Body();
                mainPart.Document.Body = body;
                EnsureSectionProperties(body, mainPart, title);

                DocxHtmlConverter converter = new(body, mainPart, mainPart.NumberingDefinitionsPart!, _logger, _fetchRemoteImages, _httpClientFactory);

                bool hasExplicitBreakRules = resolved.ChapterBreakRules is not null && resolved.ChapterBreakRules.Count > 0;
                bool breakOnH1 = ExportHelpers.HasChapterBreak(resolved, "h1");
                bool breakOnSection = ExportHelpers.HasChapterBreak(resolved, "section") || !hasExplicitBreakRules;
                bool hasContent = false;
                bool hasRenderedSection = false;

                if (resolved.IncludeTitlePage)
                {
                    converter.AppendHeading(title, 1, pageBreakBefore: false);
                    converter.AppendPageBreak();
                }

                if (resolved.IncludeToc)
                {
                    AppendTocField(body);
                    converter.AppendPageBreak();
                }

                foreach (Section section in ExportHelpers.GetOrderedSections(document))
                {
                    if (breakOnSection && hasRenderedSection)
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

                    hasRenderedSection = true;
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

            AbstractNum bulletAbstract = new() { AbstractNumberId = 1 };
            AbstractNum orderedAbstract = new() { AbstractNumberId = 2 };

            for (int level = 0; level <= 8; level++)
            {
                int left = 720 + (level * 360);
                RunFonts bulletFonts = new()
                {
                    Ascii = "Symbol",
                    HighAnsi = "Symbol",
                    ComplexScript = "Symbol"
                };
                Level bulletLevel = new(
                    new StartNumberingValue { Val = 1 },
                    new NumberingFormat { Val = NumberFormatValues.Bullet },
                    new LevelText { Val = "" },
                    new LevelJustification { Val = LevelJustificationValues.Left },
                    new NumberingSymbolRunProperties(bulletFonts),
                    new ParagraphProperties(new Indentation { Left = left.ToString(), Hanging = "360" }))
                { LevelIndex = level };
                bulletAbstract.AppendChild(bulletLevel);

                string levelText = string.Join('.', Enumerable.Range(1, level + 1).Select(i => $"%{i}")) + ".";
                Level orderedLevel = new(
                    new StartNumberingValue { Val = 1 },
                    new NumberingFormat { Val = NumberFormatValues.Decimal },
                    new LevelText { Val = levelText },
                    new LevelJustification { Val = LevelJustificationValues.Left },
                    new ParagraphProperties(new Indentation { Left = left.ToString(), Hanging = "360" }))
                { LevelIndex = level };
                orderedAbstract.AppendChild(orderedLevel);
            }

            Numbering numbering = new();
            numbering.Append(bulletAbstract);
            numbering.Append(orderedAbstract);

            part.Numbering = numbering;
            part.Numbering.Save();
            return part;
        }

        private static void EnsureStyleDefaults(MainDocumentPart mainPart)
        {
            StyleDefinitionsPart stylePart = mainPart.StyleDefinitionsPart ?? mainPart.AddNewPart<StyleDefinitionsPart>();
            Styles styles = stylePart.Styles ?? new Styles();

            Style? normal = styles.Elements<Style>()
                .FirstOrDefault(style => string.Equals(style.StyleId?.Value, "Normal", StringComparison.OrdinalIgnoreCase));
            if (normal is null)
            {
                normal = new Style
                {
                    Type = StyleValues.Paragraph,
                    StyleId = "Normal",
                    Default = true
                };
                normal.Append(new StyleName { Val = "Normal" });
                styles.Append(normal);
            }

            RunFonts runFonts = new()
            {
                Ascii = "Calibri",
                HighAnsi = "Calibri",
                ComplexScript = "Calibri"
            };
            StyleRunProperties runProps = new(
                runFonts,
                new FontSize { Val = "22" },
                new FontSizeComplexScript { Val = "22" });

            SpacingBetweenLines spacing = new()
            {
                After = "160",
                Line = "276",
                LineRule = LineSpacingRuleValues.Auto
            };
            StyleParagraphProperties paraProps = new(spacing);

            normal.StyleRunProperties = runProps;
            normal.StyleParagraphProperties = paraProps;

            EnsureHeadingStyle(styles, "Heading1", "Heading 1", "32", 0);
            EnsureHeadingStyle(styles, "Heading2", "Heading 2", "28", 1);
            EnsureHeadingStyle(styles, "Heading3", "Heading 3", "24", 2);

            stylePart.Styles = styles;
            stylePart.Styles.Save();
        }

        private static void EnsureHeadingStyle(Styles styles, string styleId, string styleName, string fontSize, int outlineLevel)
        {
            Style? style = styles.Elements<Style>()
                .FirstOrDefault(candidate => string.Equals(candidate.StyleId?.Value, styleId, StringComparison.OrdinalIgnoreCase));
            if (style is null)
            {
                style = new Style
                {
                    Type = StyleValues.Paragraph,
                    StyleId = styleId
                };
                style.Append(new StyleName { Val = styleName });
                style.Append(new BasedOn { Val = "Normal" });
                style.Append(new NextParagraphStyle { Val = "Normal" });
                style.Append(new UIPriority { Val = 9 });
                style.Append(new PrimaryStyle());
                style.Append(new UnhideWhenUsed());
                style.Append(new Rsid { Val = "00000000" });
                styles.Append(style);
            }

            style.StyleRunProperties = new StyleRunProperties(
                new Bold(),
                new FontSize { Val = fontSize },
                new FontSizeComplexScript { Val = fontSize });

            style.StyleParagraphProperties = new StyleParagraphProperties(
                new KeepNext(),
                new KeepLines(),
                new SpacingBetweenLines { Before = "200", After = "120" },
                new OutlineLevel { Val = outlineLevel });
        }

        private static void EnsureSectionProperties(Body body, MainDocumentPart mainPart, string documentTitle)
        {
            if (body.Elements<SectionProperties>().Any())
            {
                return;
            }

            HeaderPart headerPart = mainPart.AddNewPart<HeaderPart>();
            headerPart.Header = BuildHeader(documentTitle);
            headerPart.Header.Save();

            FooterPart footerPart = mainPart.AddNewPart<FooterPart>();
            footerPart.Footer = BuildFooter();
            footerPart.Footer.Save();

            string headerRelId = mainPart.GetIdOfPart(headerPart);
            string footerRelId = mainPart.GetIdOfPart(footerPart);

            SectionProperties props = new();
            props.Append(new HeaderReference { Type = HeaderFooterValues.Default, Id = headerRelId });
            props.Append(new FooterReference { Type = HeaderFooterValues.Default, Id = footerRelId });
            props.Append(new PageMargin
            {
                Top = 1440,
                Bottom = 1440,
                Left = 1440,
                Right = 1440,
                Header = 720,
                Footer = 720,
                Gutter = 0
            });
            body.Append(props);
        }

        private static Header BuildHeader(string documentTitle)
        {
            Header header = new();
            if (!string.IsNullOrWhiteSpace(documentTitle))
            {
                Paragraph paragraph = new();
                paragraph.Append(new Run(new Text(documentTitle)));
                header.Append(paragraph);
            }

            return header;
        }

        private static Footer BuildFooter()
        {
            Footer footer = new();
            Paragraph paragraph = new();
            paragraph.Append(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
            SimpleField pageField = new() { Instruction = "PAGE" };
            pageField.Append(new Run(new Text("1")));
            paragraph.Append(pageField);
            footer.Append(paragraph);
            return footer;
        }

        private static void AppendTocField(Body body)
        {
            // Word renders and updates this TOC field from Heading1-Heading3 paragraphs.
            Paragraph titleParagraph = new();
            Run titleRun = new(new Text("Table of Contents"));
            titleRun.RunProperties = new RunProperties(new Bold());
            titleParagraph.Append(titleRun);
            body.Append(titleParagraph);

            Paragraph fieldParagraph = new();
            SimpleField tocField = new()
            {
                Instruction = "TOC \\o \"1-3\" \\h \\z \\u"
            };
            tocField.Append(new Run(new Text(" ")));
            fieldParagraph.Append(tocField);
            body.Append(fieldParagraph);
        }

        private sealed class DocxHtmlConverter
        {
            private readonly Body _body;
            private readonly MainDocumentPart _mainPart;
            private readonly NumberingDefinitionsPart _numberingPart;
            private readonly ILogger _logger;
            private readonly bool _fetchRemoteImages;
            private readonly IHttpClientFactory _httpClientFactory;
            private readonly HtmlParser _parser = new();
            private readonly Stack<ListContext> _listStack = new();
            private int _nextNumberingId;
            private int _listDepth;

            public DocxHtmlConverter(
                Body body,
                MainDocumentPart mainPart,
                NumberingDefinitionsPart numberingPart,
                ILogger logger,
                bool fetchRemoteImages,
                IHttpClientFactory httpClientFactory)
            {
                _body = body ?? throw new ArgumentNullException(nameof(body));
                _mainPart = mainPart ?? throw new ArgumentNullException(nameof(mainPart));
                _numberingPart = numberingPart ?? throw new ArgumentNullException(nameof(numberingPart));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _fetchRemoteImages = fetchRemoteImages;
                _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
                _nextNumberingId = (_numberingPart.Numbering?.Elements<NumberingInstance>()
                    .Select(instance => (int?)instance.NumberID?.Value)
                    .Max() ?? 0) + 1;
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
                            AppendList(element, tag == "ol", breakOnH1, ref hasContent);
                            continue;
                        }

                        if (tag is "table")
                        {
                            AppendTable(element);
                            hasContent = true;
                            continue;
                        }

                        if (tag is "blockquote")
                        {
                            AppendBlockquote(element);
                            hasContent = true;
                            continue;
                        }

                        if (tag is "pre")
                        {
                            AppendCodeBlock(element);
                            hasContent = true;
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
                            Paragraph imageParagraph = new();
                            if (!TryAppendImage(imageParagraph, element))
                            {
                                string src = element.GetAttribute("src") ?? string.Empty;
                                AppendRun(imageParagraph, string.IsNullOrWhiteSpace(src) ? "[Image omitted]" : $"[Image: {src}]", new InlineStyle());
                            }

                            _body.Append(imageParagraph);
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

            private void AppendTable(IElement tableElement)
            {
                List<IElement> rows = tableElement.QuerySelectorAll("tr").OfType<IElement>().ToList();
                if (rows.Count == 0)
                {
                    return;
                }

                int maxColumns = rows.Max(GetRowColumnCount);
                List<int> columnWidthsTwips = ResolveColumnWidthsTwips(tableElement, maxColumns);
                bool hasColumnWidths = columnWidthsTwips.Count > 0;

                Table table = new();
                TableProperties props = new(
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 4 },
                        new BottomBorder { Val = BorderValues.Single, Size = 4 },
                        new LeftBorder { Val = BorderValues.Single, Size = 4 },
                        new RightBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }));
                if (hasColumnWidths)
                {
                    props.Append(new TableLayout { Type = TableLayoutValues.Fixed });
                }

                table.AppendChild(props);

                if (maxColumns > 0)
                {
                    TableGrid tableGrid = new();
                    for (int index = 0; index < maxColumns; index++)
                    {
                        GridColumn column = new();
                        if (index < columnWidthsTwips.Count)
                        {
                            column.Width = columnWidthsTwips[index].ToString(CultureInfo.InvariantCulture);
                        }

                        tableGrid.Append(column);
                    }

                    table.Append(tableGrid);
                }

                foreach (IElement rowElement in rows)
                {
                    TableRow row = new();
                    int? rowHeightTwips = ResolveRowHeightTwips(rowElement);
                    if (rowHeightTwips.HasValue)
                    {
                        row.Append(new TableRowProperties(
                            new TableRowHeight
                            {
                                Val = (UInt32Value)(uint)Math.Max(0, rowHeightTwips.Value),
                                HeightType = HeightRuleValues.AtLeast
                            }));
                    }

                    int columnIndex = 0;
                    foreach (IElement cellElement in rowElement.Children.Where(c =>
                                 c.TagName.Equals("TD", StringComparison.OrdinalIgnoreCase)
                                 || c.TagName.Equals("TH", StringComparison.OrdinalIgnoreCase)))
                    {
                        int colspan = ParsePositiveInt(cellElement.GetAttribute("colspan")) ?? 1;
                        int? rowspan = ParsePositiveInt(cellElement.GetAttribute("rowspan"));
                        if (rowspan.HasValue && rowspan.Value > 1)
                        {
                            _logger.LogDebug("[DOCX] Table row span not fully supported; rendering current row without vertical merge.");
                        }

                        TableCell cell = new();
                        TableCellProperties cellProps = new();

                        int? cellWidthTwips = ResolveCellWidthTwips(cellElement, columnWidthsTwips, columnIndex);
                        if (cellWidthTwips.HasValue)
                        {
                            cellProps.Append(new TableCellWidth
                            {
                                Type = TableWidthUnitValues.Dxa,
                                Width = cellWidthTwips.Value.ToString(CultureInfo.InvariantCulture)
                            });
                        }
                        else
                        {
                            cellProps.Append(new TableCellWidth { Type = TableWidthUnitValues.Auto });
                        }

                        if (colspan > 1)
                        {
                            cellProps.Append(new GridSpan { Val = colspan });
                        }

                        bool isHeaderCell = cellElement.TagName.Equals("TH", StringComparison.OrdinalIgnoreCase);
                        string? fill = ResolveCellFillHex(cellElement, isHeaderCell);
                        if (!string.IsNullOrWhiteSpace(fill))
                        {
                            cellProps.Append(new Shading
                            {
                                Val = ShadingPatternValues.Clear,
                                Color = "auto",
                                Fill = fill
                            });
                        }

                        cell.Append(cellProps);

                        Paragraph cellParagraph = new();
                        AppendInlineNodes(cellElement.ChildNodes, cellParagraph, new InlineStyle());
                        cell.Append(cellParagraph);
                        row.Append(cell);

                        columnIndex += Math.Max(1, colspan);
                    }

                    table.Append(row);
                }

                _body.Append(table);
            }

            private static List<int> ResolveColumnWidthsTwips(IElement tableElement, int maxColumns)
            {
                const int maxTableWidthTwips = 9360; // ~6.5in writable width
                const int minColumnWidthTwips = 720; // 0.5in guardrail

                List<int?> widths = Enumerable.Repeat<int?>(null, Math.Max(0, maxColumns)).ToList();
                if (maxColumns <= 0)
                {
                    return new List<int>();
                }

                List<IElement> columns = tableElement.QuerySelectorAll("col").OfType<IElement>().ToList();
                for (int index = 0; index < columns.Count && index < widths.Count; index++)
                {
                    widths[index] = ResolveWidthTwips(columns[index]);
                }

                if (widths.All(width => !width.HasValue))
                {
                    IElement? firstRow = tableElement.QuerySelectorAll("tr").OfType<IElement>().FirstOrDefault();
                    if (firstRow is not null)
                    {
                        int columnIndex = 0;
                        foreach (IElement cell in firstRow.Children.Where(c =>
                                     c.TagName.Equals("TD", StringComparison.OrdinalIgnoreCase)
                                     || c.TagName.Equals("TH", StringComparison.OrdinalIgnoreCase)))
                        {
                            int colspan = ParsePositiveInt(cell.GetAttribute("colspan")) ?? 1;
                            int? width = ResolveWidthTwips(cell);
                            for (int offset = 0; offset < colspan && columnIndex + offset < widths.Count; offset++)
                            {
                                widths[columnIndex + offset] = width;
                            }

                            columnIndex += Math.Max(1, colspan);
                        }
                    }
                }

                int knownCount = widths.Count(width => width.HasValue && width.Value > 0);
                int knownTotal = widths.Where(width => width.HasValue && width.Value > 0).Sum(width => width!.Value);

                int fallbackWidth = knownCount > 0
                    ? Math.Max(minColumnWidthTwips, knownTotal / knownCount)
                    : Math.Max(minColumnWidthTwips, maxTableWidthTwips / maxColumns);

                List<int> normalized = widths
                    .Select(width => Math.Max(minColumnWidthTwips, width ?? fallbackWidth))
                    .ToList();

                int totalWidth = normalized.Sum();
                if (totalWidth > maxTableWidthTwips && totalWidth > 0)
                {
                    double scale = (double)maxTableWidthTwips / totalWidth;
                    normalized = normalized
                        .Select(width => Math.Max(minColumnWidthTwips, (int)Math.Round(width * scale)))
                        .ToList();
                }

                return normalized;
            }

            private static int GetRowColumnCount(IElement rowElement)
            {
                int count = 0;
                foreach (IElement cellElement in rowElement.Children.Where(c =>
                             c.TagName.Equals("TD", StringComparison.OrdinalIgnoreCase)
                             || c.TagName.Equals("TH", StringComparison.OrdinalIgnoreCase)))
                {
                    count += ParsePositiveInt(cellElement.GetAttribute("colspan")) ?? 1;
                }

                return Math.Max(1, count);
            }

            private static int? ResolveCellWidthTwips(IElement cellElement, IReadOnlyList<int> columnWidthsTwips, int columnIndex)
            {
                int? cellWidth = ResolveWidthTwips(cellElement);
                if (cellWidth.HasValue)
                {
                    return cellWidth;
                }

                if (columnIndex >= 0 && columnIndex < columnWidthsTwips.Count)
                {
                    return columnWidthsTwips[columnIndex];
                }

                return null;
            }

            private static int? ResolveWidthTwips(IElement element)
            {
                string? dataColWidth = element.GetAttribute("data-colwidth") ?? element.GetAttribute("colwidth");
                if (!string.IsNullOrWhiteSpace(dataColWidth))
                {
                    string first = dataColWidth.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
                    int? parsed = ParseLengthToTwips(first);
                    if (parsed.HasValue)
                    {
                        return parsed;
                    }
                }

                string? width = GetStyleValue(element, "width")
                    ?? GetStyleValue(element, "min-width")
                    ?? element.GetAttribute("width");
                return ParseLengthToTwips(width);
            }

            private static int? ResolveRowHeightTwips(IElement rowElement)
            {
                string? value = GetStyleValue(rowElement, "height")
                    ?? GetStyleValue(rowElement, "min-height")
                    ?? rowElement.GetAttribute("height");
                return ParseLengthToTwips(value);
            }

            private static int? ParseLengthToTwips(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                string normalized = value.Trim().ToLowerInvariant();
                if (normalized.EndsWith("%", StringComparison.Ordinal))
                {
                    return null;
                }

                double factor = 15d; // px -> twips at 96 DPI
                if (normalized.EndsWith("px", StringComparison.Ordinal))
                {
                    normalized = normalized[..^2].Trim();
                    factor = 15d;
                }
                else if (normalized.EndsWith("pt", StringComparison.Ordinal))
                {
                    normalized = normalized[..^2].Trim();
                    factor = 20d;
                }
                else if (normalized.EndsWith("in", StringComparison.Ordinal))
                {
                    normalized = normalized[..^2].Trim();
                    factor = 1440d;
                }
                else if (normalized.EndsWith("cm", StringComparison.Ordinal))
                {
                    normalized = normalized[..^2].Trim();
                    factor = 1440d / 2.54d;
                }
                else if (normalized.EndsWith("mm", StringComparison.Ordinal))
                {
                    normalized = normalized[..^2].Trim();
                    factor = 1440d / 25.4d;
                }

                if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double numeric) || numeric <= 0)
                {
                    return null;
                }

                return (int)Math.Round(numeric * factor);
            }

            private static int? ParsePositiveInt(string? value)
            {
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) || parsed <= 0)
                {
                    return null;
                }

                return parsed;
            }

            private static string? ResolveCellFillHex(IElement cellElement, bool isHeaderCell)
            {
                string? color = GetStyleValue(cellElement, "background-color")
                    ?? GetStyleValue(cellElement, "background");
                string? normalized = NormalizeHexColor(color);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return normalized;
                }

                return isHeaderCell ? "D9D9D9" : null;
            }

            private static string? GetStyleValue(IElement element, string propertyName)
            {
                string? style = element.GetAttribute("style");
                if (string.IsNullOrWhiteSpace(style))
                {
                    return null;
                }

                string prefix = $"{propertyName}:";
                foreach (string part in style.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return part[prefix.Length..].Trim();
                    }
                }

                return null;
            }

            private static string? NormalizeHexColor(string? raw)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                string value = raw.Trim();
                int bang = value.IndexOf('!');
                if (bang >= 0)
                {
                    value = value[..bang].Trim();
                }

                if (value.StartsWith("#", StringComparison.Ordinal))
                {
                    string hex = value[1..].Trim();
                    if (hex.Length == 3)
                    {
                        hex = string.Concat(hex.Select(ch => $"{ch}{ch}"));
                    }

                    if (hex.Length == 6 && hex.All(Uri.IsHexDigit))
                    {
                        return hex.ToUpperInvariant();
                    }

                    return null;
                }

                if (value.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(')'))
                {
                    string payload = value[4..^1];
                    string[] parts = payload.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length == 3
                        && byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte r)
                        && byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte g)
                        && byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b))
                    {
                        return $"{r:X2}{g:X2}{b:X2}";
                    }
                }

                return value.ToLowerInvariant() switch
                {
                    "black" => "000000",
                    "white" => "FFFFFF",
                    "gray" or "grey" => "808080",
                    "lightgray" or "lightgrey" => "D3D3D3",
                    "red" => "FF0000",
                    "green" => "008000",
                    "blue" => "0000FF",
                    _ => null
                };
            }

            private void AppendBlockquote(IElement element)
            {
                ParagraphProperties props = new(
                    new Indentation { Left = "720" },
                    new SpacingBetweenLines { After = "160" });
                Paragraph paragraph = new();
                paragraph.Append(props);
                AppendInlineNodes(element.ChildNodes, paragraph, new InlineStyle(Italic: true));
                _body.Append(paragraph);
            }

            private void AppendCodeBlock(IElement element)
            {
                string text = element.TextContent ?? string.Empty;
                string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal).Split('\n');

                Paragraph paragraph = new();
                ParagraphProperties props = new(
                    new SpacingBetweenLines { After = "120", Line = "240", LineRule = LineSpacingRuleValues.Auto });
                paragraph.Append(props);

                RunProperties runProps = new(
                    new RunFonts
                    {
                        Ascii = "Consolas",
                        HighAnsi = "Consolas",
                        ComplexScript = "Consolas"
                    },
                    new FontSize { Val = "20" },
                    new FontSizeComplexScript { Val = "20" });

                for (int i = 0; i < lines.Length; i++)
                {
                    Run run = new();
                    run.Append(runProps.CloneNode(true));
                    Text codeText = new(lines[i]) { Space = SpaceProcessingModeValues.Preserve };
                    run.Append(codeText);
                    paragraph.Append(run);
                    if (i < lines.Length - 1)
                    {
                        paragraph.Append(new Run(new Break()));
                    }
                }

                _body.Append(paragraph);
            }

            private void AppendList(IElement list, bool ordered, bool breakOnH1, ref bool hasContent)
            {
                int? startValue = null;
                if (ordered && list.HasAttribute("start"))
                {
                    if (int.TryParse(list.GetAttribute("start"), out int start) && start > 0)
                    {
                        startValue = start;
                    }
                }

                int numId = CreateNumberingInstance(ordered, startValue);
                int baseDepth = _listStack.Count == 0 ? _listDepth : _listStack.Peek().BaseDepth;
                _listDepth++;
                _listStack.Push(new ListContext(numId, baseDepth));

                foreach (IElement item in list.Children.Where(child => child.TagName.Equals("LI", StringComparison.OrdinalIgnoreCase)))
                {
                    AppendListItem(item, ordered, breakOnH1, ref hasContent);
                }

                _listStack.Pop();
                _listDepth = Math.Max(0, _listDepth - 1);
            }

            private void AppendListItem(IElement item, bool ordered, bool breakOnH1, ref bool hasContent)
            {
                int baseDepth = _listStack.Count > 0 ? _listStack.Peek().BaseDepth : 0;
                int level = Math.Max(0, _listDepth - 1 - baseDepth);
                int clampedLevel = Math.Clamp(level, 0, 8);
                if (level > 8)
                {
                    _logger.LogDebug("[DOCX] List depth clamped to 8 (depth={Depth}).", level);
                }

                IEnumerable<IElement> blockChildren = item.Children.Where(child =>
                    child.TagName.Equals("P", StringComparison.OrdinalIgnoreCase)
                    || child.TagName.Equals("H1", StringComparison.OrdinalIgnoreCase)
                    || child.TagName.Equals("H2", StringComparison.OrdinalIgnoreCase)
                    || child.TagName.Equals("H3", StringComparison.OrdinalIgnoreCase));

                bool wroteParagraph = false;
                foreach (IElement block in blockChildren)
                {
                    AppendListParagraph(block.ChildNodes, ordered, clampedLevel);
                    wroteParagraph = true;
                    hasContent = true;
                }

                IEnumerable<INode> inlineNodes = item.ChildNodes.Where(node =>
                    node is not IElement el
                    || !el.TagName.Equals("UL", StringComparison.OrdinalIgnoreCase)
                    && !el.TagName.Equals("OL", StringComparison.OrdinalIgnoreCase));

                if (!wroteParagraph && inlineNodes.Any(node => node is IText text && !string.IsNullOrWhiteSpace(text.Text) || node is IElement))
                {
                    AppendListParagraph(inlineNodes, ordered, clampedLevel);
                    hasContent = true;
                }

                foreach (IElement nested in item.Children.Where(child =>
                             child.TagName.Equals("UL", StringComparison.OrdinalIgnoreCase)
                             || child.TagName.Equals("OL", StringComparison.OrdinalIgnoreCase)))
                {
                    AppendList(nested, nested.TagName.Equals("OL", StringComparison.OrdinalIgnoreCase), breakOnH1, ref hasContent);
                }
            }

            private void AppendListParagraph(IEnumerable<INode> inlineNodes, bool ordered, int level)
            {
                int numId = _listStack.Count > 0 ? _listStack.Peek().NumberingId : CreateNumberingInstance(ordered, null);
                ParagraphProperties props = new();
                props.Append(new NumberingProperties(
                    new NumberingLevelReference { Val = level },
                    new NumberingId { Val = numId }));

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

            private void AppendInlineNodes(IEnumerable<INode> nodes, OpenXmlCompositeElement container, InlineStyle style)
            {
                foreach (INode node in nodes)
                {
                    if (node is IText textNode)
                    {
                        AppendRun(container, textNode.Text, style);
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
                        container.Append(new Run(new Break()));
                        continue;
                    }
                    else if (tag is "img")
                    {
                        if (!TryAppendImage(container, element))
                        {
                            AppendRun(container, "[Image omitted]", style);
                        }
                        continue;
                    }
                    else if (tag is "a")
                    {
                        string? href = element.GetAttribute("href");
                        if (string.IsNullOrWhiteSpace(href) || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogDebug("[DOCX] Skipping unsafe hyperlink href={Href}", href);
                            AppendInlineNodes(element.ChildNodes, container, style);
                            continue;
                        }

                        if (!Uri.TryCreate(href, UriKind.Absolute, out Uri? uri) ||
                            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeMailto))
                        {
                            _logger.LogDebug("[DOCX] Skipping invalid or unsupported hyperlink href={Href}", href);
                            AppendInlineNodes(element.ChildNodes, container, style);
                            continue;
                        }

                        HyperlinkRelationship rel = _mainPart.AddHyperlinkRelationship(uri, true);
                        _logger.LogDebug("[DOCX] Hyperlink created href={Href} rId={RelId}", href, rel.Id);
                        Hyperlink hyperlink = new() { Id = rel.Id };
                        container.Append(hyperlink);
                        AppendInlineNodes(element.ChildNodes, hyperlink, style with { IsHyperlink = true });
                        continue;
                    }

                    AppendInlineNodes(element.ChildNodes, container, nextStyle);
                }
            }

            private void AppendRun(OpenXmlCompositeElement container, string? text, InlineStyle style)
            {
                if (container is null)
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

                if (style.IsHyperlink)
                {
                    props.Append(new RunStyle { Val = "Hyperlink" });
                    if (!style.Underline)
                    {
                        props.Append(new Underline { Val = UnderlineValues.Single });
                    }

                    props.Append(new Color { Val = "0000FF" });
                }

                if (props.ChildElements.Count > 0)
                {
                    run.Append(props);
                }

                Text textNode = new(value);
                if (ShouldPreserveSpace(value))
                {
                    textNode.Space = SpaceProcessingModeValues.Preserve;
                    _logger.LogDebug("[DOCX] Preserve whitespace for text node.");
                }
                run.Append(textNode);
                container.Append(run);
            }

            private bool TryAppendImage(OpenXmlCompositeElement container, IElement element)
            {
                string? src = element.GetAttribute("src");
                if (string.IsNullOrWhiteSpace(src))
                {
                    return false;
                }

                try
                {
                    if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseDataUri(src, out byte[] imageBytes, out PartTypeInfo imagePartType))
                        {
                            _logger.LogDebug("[DOCX] Image data URI parse failed.");
                            return false;
                        }

                        return AppendImageBytes(container, imageBytes, imagePartType);
                    }

                    if (!_fetchRemoteImages)
                    {
                        _logger.LogDebug("[DOCX] Remote image fetch disabled.");
                        return false;
                    }

                    if (!Uri.TryCreate(src, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps)
                    {
                        _logger.LogDebug("[DOCX] Remote image URL not allowed: {Url}", src);
                        return false;
                    }

                    byte[] bytes = FetchRemoteImage(uri);
                    if (!TryGetImagePartType(bytes, out PartTypeInfo partType))
                    {
                        _logger.LogDebug("[DOCX] Remote image type not supported.");
                        return false;
                    }

                    return AppendImageBytes(container, bytes, partType);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[DOCX] Image insert failed.");
                    return false;
                }
            }

            private bool AppendImageBytes(OpenXmlCompositeElement container, byte[] bytes, PartTypeInfo partType)
            {
                if (!TryGetImageDimensions(bytes, out int widthPx, out int heightPx))
                {
                    _logger.LogDebug("[DOCX] Image dimensions not detected.");
                    return false;
                }

                const long emusPerInch = 914400;
                const long emusPerPx = 9525; // 96 DPI
                long widthEmu = widthPx * emusPerPx;
                long heightEmu = heightPx * emusPerPx;
                long maxWidthEmu = (long)(6.5 * emusPerInch);
                if (widthEmu > maxWidthEmu)
                {
                    double scale = (double)maxWidthEmu / widthEmu;
                    widthEmu = maxWidthEmu;
                    heightEmu = (long)(heightEmu * scale);
                }

                ImagePart imagePart = _mainPart.AddImagePart(partType);
                using (MemoryStream stream = new(bytes))
                {
                    imagePart.FeedData(stream);
                }
                string relId = _mainPart.GetIdOfPart(imagePart);
                Drawing drawing = BuildImageDrawing(relId, widthEmu, heightEmu);
                container.Append(new Run(drawing));

                _logger.LogInformation("[DOCX] Image inserted type={Type} bytes={Bytes} size={Width}x{Height}px.",
                    partType, bytes.Length, widthPx, heightPx);
                return true;
            }

            private byte[] FetchRemoteImage(Uri uri)
            {
                using HttpClient client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                using HttpResponseMessage response = client.GetAsync(uri).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
                byte[] bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                if (bytes.Length > 5 * 1024 * 1024)
                {
                    throw new InvalidOperationException("Remote image exceeded size limit.");
                }

                return bytes;
            }

            private static bool TryParseDataUri(string dataUri, out byte[] bytes, out PartTypeInfo partType)
            {
                bytes = Array.Empty<byte>();
                partType = ImagePartType.Png;

                int commaIndex = dataUri.IndexOf(',');
                if (commaIndex < 0)
                {
                    return false;
                }

                string header = dataUri.Substring(5, commaIndex - 5);
                string payload = dataUri[(commaIndex + 1)..];
                if (!header.Contains("base64", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (header.Contains("image/png", StringComparison.OrdinalIgnoreCase))
                {
                    partType = ImagePartType.Png;
                }
                else if (header.Contains("image/jpeg", StringComparison.OrdinalIgnoreCase) || header.Contains("image/jpg", StringComparison.OrdinalIgnoreCase))
                {
                    partType = ImagePartType.Jpeg;
                }
                else if (header.Contains("image/gif", StringComparison.OrdinalIgnoreCase))
                {
                    partType = ImagePartType.Gif;
                }
                else
                {
                    return false;
                }

                bytes = Convert.FromBase64String(payload);
                return true;
            }

            private static bool TryGetImagePartType(byte[] bytes, out PartTypeInfo partType)
            {
                partType = ImagePartType.Png;
                if (bytes.Length >= 8 &&
                    bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                {
                    partType = ImagePartType.Png;
                    return true;
                }

                if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8)
                {
                    partType = ImagePartType.Jpeg;
                    return true;
                }

                if (bytes.Length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
                {
                    partType = ImagePartType.Gif;
                    return true;
                }

                return false;
            }

            private static bool TryGetImageDimensions(byte[] bytes, out int width, out int height)
            {
                width = 0;
                height = 0;

                if (bytes.Length >= 24 &&
                    bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                {
                    width = (bytes[16] << 24) + (bytes[17] << 16) + (bytes[18] << 8) + bytes[19];
                    height = (bytes[20] << 24) + (bytes[21] << 16) + (bytes[22] << 8) + bytes[23];
                    return true;
                }

                if (bytes.Length >= 10 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
                {
                    width = bytes[6] + (bytes[7] << 8);
                    height = bytes[8] + (bytes[9] << 8);
                    return true;
                }

                if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8)
                {
                    int index = 2;
                    while (index + 9 < bytes.Length)
                    {
                        if (bytes[index] != 0xFF)
                        {
                            index++;
                            continue;
                        }

                        byte marker = bytes[index + 1];
                        if (marker == 0xC0 || marker == 0xC2)
                        {
                            height = (bytes[index + 5] << 8) + bytes[index + 6];
                            width = (bytes[index + 7] << 8) + bytes[index + 8];
                            return true;
                        }

                        int length = (bytes[index + 2] << 8) + bytes[index + 3];
                        if (length <= 0)
                        {
                            break;
                        }

                        index += 2 + length;
                    }
                }

                return false;
            }

            private static Drawing BuildImageDrawing(string relationshipId, long widthEmu, long heightEmu)
            {
                return new Drawing(
                    new Wp.Inline(
                        new Wp.Extent { Cx = widthEmu, Cy = heightEmu },
                        new Wp.EffectExtent
                        {
                            LeftEdge = 0L,
                            TopEdge = 0L,
                            RightEdge = 0L,
                            BottomEdge = 0L
                        },
                        new Wp.DocProperties { Id = (UInt32Value)1U, Name = "Picture" },
                        new Wp.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                        new A.Graphic(
                            new A.GraphicData(
                                new Pic.Picture(
                                    new Pic.NonVisualPictureProperties(
                                        new Pic.NonVisualDrawingProperties { Id = (UInt32Value)0U, Name = "Picture" },
                                        new Pic.NonVisualPictureDrawingProperties()),
                                    new Pic.BlipFill(
                                        new A.Blip { Embed = relationshipId },
                                        new A.Stretch(new A.FillRectangle())),
                                    new Pic.ShapeProperties(
                                        new A.Transform2D(
                                            new A.Offset { X = 0L, Y = 0L },
                                            new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                        new A.PresetGeometry(new A.AdjustValueList())
                                        { Preset = A.ShapeTypeValues.Rectangle })))
                            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                    {
                        DistanceFromTop = 0U,
                        DistanceFromBottom = 0U,
                        DistanceFromLeft = 0U,
                        DistanceFromRight = 0U
                    });
            }

            private static bool ShouldPreserveSpace(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return false;
                }

                return char.IsWhiteSpace(value[0])
                       || char.IsWhiteSpace(value[^1])
                       || value.Contains("  ", StringComparison.Ordinal)
                       || value.Contains('\u00A0', StringComparison.Ordinal);
            }

            private readonly record struct InlineStyle(
                bool Bold = false,
                bool Italic = false,
                bool Underline = false,
                bool IsHyperlink = false);
            private readonly record struct ListContext(int NumberingId, int BaseDepth);

            private int CreateNumberingInstance(bool ordered, int? startValue)
            {
                int abstractNumId = ordered ? 2 : 1;
                int numId = _nextNumberingId++;
                NumberingInstance instance = new() { NumberID = numId };
                instance.Append(new AbstractNumId { Val = abstractNumId });
                if (ordered && startValue.HasValue && startValue.Value > 1)
                {
                    LevelOverride levelOverride = new() { LevelIndex = 0 };
                    levelOverride.Append(new StartOverrideNumberingValue { Val = startValue.Value });
                    instance.Append(levelOverride);
                }

                _numberingPart.Numbering ??= new Numbering();
                _numberingPart.Numbering.Append(instance);
                _numberingPart.Numbering.Save();

                _logger.LogInformation("[DOCX] List instance created numId={NumId} type={Type} start={Start}.",
                    numId,
                    ordered ? "ol" : "ul",
                    startValue ?? 1);

                return numId;
            }
        }
    }
}

