using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using WriterApp.Domain.Documents;
using WriterDocument = WriterApp.Domain.Documents.Document;

namespace WriterApp.Application.Exporting
{
    public sealed class EpubExportRenderer : IExportRenderer
    {
        public ExportFormat Format => ExportFormat.Epub;
        public ExportKind Kind => ExportKind.Document;

        private const string ContainerXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                                            "<container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\">\n" +
                                            "  <rootfiles>\n" +
                                            "    <rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\" />\n" +
                                            "  </rootfiles>\n" +
                                            "</container>\n";

        public Task<ExportResult> RenderAsync(WriterDocument document, ExportOptions options)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            ExportOptions resolved = options ?? new ExportOptions();
            string title = ExportHelpers.GetDocumentTitle(document);
            string author = string.IsNullOrWhiteSpace(document.Metadata.Author) ? "Unknown" : document.Metadata.Author.Trim();
            string language = string.IsNullOrWhiteSpace(document.Metadata.Language) ? "en" : document.Metadata.Language.Trim();
            string identifier = $"urn:uuid:{document.DocumentId}";
            string modified = document.Metadata.ModifiedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");

            bool splitOnH1 = ExportHelpers.HasChapterBreak(resolved, "h1");
            List<EpubChapter> chapters = BuildChapters(document, splitOnH1);
            EpubAsset? coverAsset = TryBuildCoverAsset(resolved);
            if (coverAsset is not null)
            {
                chapters.Insert(0, new EpubChapter(
                    "cover.xhtml",
                    "Cover",
                    $"<section class=\"book-cover-page\"><img src=\"../images/{coverAsset.FileName}\" alt=\"Project cover\" /></section>",
                    "cover"));
            }

            if (chapters.Count == 0)
            {
                chapters.Add(new EpubChapter("chapter-001.xhtml", title, "<p></p>", "chap1"));
            }

            string stylesheet = BuildStylesheet();
            string nav = BuildNav(chapters, title, language);
            string ncx = BuildNcx(chapters, title, identifier);
            string opf = BuildOpf(chapters, title, author, language, identifier, modified, coverAsset);

            using MemoryStream stream = new();
            using (ZipArchive archive = new(stream, ZipArchiveMode.Create, true))
            {
                ZipArchiveEntry mimeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
                using (Stream mimeStream = mimeEntry.Open())
                using (StreamWriter writer = new(mimeStream, new UTF8Encoding(false)))
                {
                    writer.Write("application/epub+zip");
                }

                AddTextEntry(archive, "META-INF/container.xml", ContainerXml);
                AddTextEntry(archive, "OEBPS/content.opf", opf);
                AddTextEntry(archive, "OEBPS/nav.xhtml", nav);
                AddTextEntry(archive, "OEBPS/toc.ncx", ncx);
                AddTextEntry(archive, "OEBPS/styles/style.css", stylesheet);
                if (coverAsset is not null)
                {
                    AddBinaryEntry(archive, $"OEBPS/images/{coverAsset.FileName}", coverAsset.Bytes);
                }

                foreach (EpubChapter chapter in chapters)
                {
                    string xhtml = BuildChapterXhtml(chapter.Title, chapter.BodyHtml, language);
                    AddTextEntry(archive, $"OEBPS/chapters/{chapter.FileName}", xhtml);
                }
            }

            byte[] payload = stream.ToArray();
            string fileName = ExportHelpers.SanitizeFileName(document.Metadata.Title, "document", ".epub");
            ExportResult result = new(payload, "application/epub+zip", fileName);
            return Task.FromResult(result);
        }

        private static List<EpubChapter> BuildChapters(WriterDocument document, bool splitOnH1)
        {
            HtmlParser parser = new();
            List<EpubChapter> chapters = new();
            int index = 1;

            foreach (Section section in ExportHelpers.GetOrderedSections(document))
            {
                string sectionTitle = ExportHelpers.GetSectionTitle(section);
                string html = ExportHelpers.BuildSectionHtml(section.Content, sectionTitle, allowStripHeading: !splitOnH1);
                if (string.IsNullOrWhiteSpace(html))
                {
                    html = "<p></p>";
                }

                // TODO: Support embedding images in EPUB exports.
                html = ReplaceImages(parser, html);

                if (splitOnH1)
                {
                    List<(string Title, string Body)> splits = SplitByH1(parser, html, sectionTitle);
                    foreach ((string title, string body) in splits)
                    {
                        chapters.Add(BuildChapter(index++, title, body));
                    }
                }
                else
                {
                    string withHeading = $"<h2>{WebUtility.HtmlEncode(sectionTitle)}</h2>\n{html}";
                    chapters.Add(BuildChapter(index++, sectionTitle, withHeading));
                }
            }

            return chapters;
        }

        private static string ReplaceImages(HtmlParser parser, string html)
        {
            IDocument document = parser.ParseDocument($"<body>{html}</body>");
            foreach (IElement img in document.QuerySelectorAll("img"))
            {
                IElement replacement = document.CreateElement("span");
                replacement.TextContent = "[Image omitted]";
                img.Replace(replacement);
            }

            return document.Body?.InnerHtml ?? html;
        }

        private static List<(string Title, string Body)> SplitByH1(HtmlParser parser, string html, string fallbackTitle)
        {
            IDocument document = parser.ParseDocument($"<body>{html}</body>");
            IElement? body = document.Body;
            if (body is null)
            {
                return new List<(string, string)> { (fallbackTitle, $"<h1>{WebUtility.HtmlEncode(fallbackTitle)}</h1>") };
            }

            List<(string Title, string Body)> output = new();
            List<INode> currentNodes = new();
            string? currentTitle = null;

            void Flush()
            {
                if (currentNodes.Count == 0)
                {
                    return;
                }

                string title = string.IsNullOrWhiteSpace(currentTitle) ? fallbackTitle : currentTitle!;
                string bodyHtml = RenderNodes(currentNodes);
                output.Add((title, bodyHtml));
                currentNodes.Clear();
            }

            foreach (INode node in body.ChildNodes)
            {
                if (node is IElement element && element.TagName.Equals("H1", StringComparison.OrdinalIgnoreCase))
                {
                    Flush();
                    currentTitle = element.TextContent?.Trim();
                }

                currentNodes.Add(node);
            }

            Flush();

            if (output.Count == 0)
            {
                output.Add((fallbackTitle, $"<h1>{WebUtility.HtmlEncode(fallbackTitle)}</h1>\n{html}"));
            }

            return output;
        }

        private static string RenderNodes(IEnumerable<INode> nodes)
        {
            StringBuilder builder = new();
            foreach (INode node in nodes)
            {
                if (node is IElement element)
                {
                    builder.Append(element.OuterHtml);
                }
                else if (node is IText textNode)
                {
                    builder.Append(WebUtility.HtmlEncode(textNode.Text));
                }
            }

            return builder.ToString();
        }

        private static EpubChapter BuildChapter(int index, string title, string bodyHtml)
        {
            string fileName = $"chapter-{index:000}.xhtml";
            string id = $"chap{index:000}";
            return new EpubChapter(fileName, title, bodyHtml, id);
        }

        private static string BuildChapterXhtml(string title, string bodyHtml, string language)
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                   "<!DOCTYPE html>\n" +
                   $"<html xmlns=\"http://www.w3.org/1999/xhtml\" xml:lang=\"{WebUtility.HtmlEncode(language)}\">\n" +
                   "<head>\n" +
                   "  <meta charset=\"utf-8\" />\n" +
                   $"  <title>{WebUtility.HtmlEncode(title)}</title>\n" +
                   "  <link rel=\"stylesheet\" type=\"text/css\" href=\"../styles/style.css\" />\n" +
                   "</head>\n" +
                   "<body>\n" +
                   bodyHtml +
                   "\n</body>\n</html>\n";
        }

        private static string BuildNav(IEnumerable<EpubChapter> chapters, string title, string language)
        {
            StringBuilder builder = new();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n")
                .Append("<!DOCTYPE html>\n")
                .Append("<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\" xml:lang=\"")
                .Append(WebUtility.HtmlEncode(language))
                .Append("\">\n")
                .Append("<head>\n")
                .Append("  <meta charset=\"utf-8\" />\n")
                .Append($"  <title>{WebUtility.HtmlEncode(title)}</title>\n")
                .Append("</head>\n")
                .Append("<body>\n")
                .Append("  <nav epub:type=\"toc\" id=\"toc\">\n")
                .Append("    <h1>Table of Contents</h1>\n")
                .Append("    <ol>\n");

            foreach (EpubChapter chapter in chapters)
            {
                builder.Append("      <li><a href=\"chapters/")
                    .Append(chapter.FileName)
                    .Append("\">")
                    .Append(WebUtility.HtmlEncode(chapter.Title))
                    .Append("</a></li>\n");
            }

            builder.Append("    </ol>\n")
                .Append("  </nav>\n")
                .Append("</body>\n</html>\n");
            return builder.ToString();
        }

        private static string BuildNcx(IReadOnlyList<EpubChapter> chapters, string title, string identifier)
        {
            StringBuilder builder = new();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n")
                .Append("<ncx xmlns=\"http://www.daisy.org/z3986/2005/ncx/\" version=\"2005-1\">\n")
                .Append("  <head>\n")
                .Append($"    <meta name=\"dtb:uid\" content=\"{WebUtility.HtmlEncode(identifier)}\" />\n")
                .Append("    <meta name=\"dtb:depth\" content=\"1\" />\n")
                .Append("    <meta name=\"dtb:totalPageCount\" content=\"0\" />\n")
                .Append("    <meta name=\"dtb:maxPageNumber\" content=\"0\" />\n")
                .Append("  </head>\n")
                .Append("  <docTitle>\n")
                .Append($"    <text>{WebUtility.HtmlEncode(title)}</text>\n")
                .Append("  </docTitle>\n")
                .Append("  <navMap>\n");

            int playOrder = 1;
            foreach (EpubChapter chapter in chapters)
            {
                builder.Append("    <navPoint id=\"")
                    .Append(chapter.Id)
                    .Append("\" playOrder=\"")
                    .Append(playOrder++)
                    .Append("\">\n")
                    .Append("      <navLabel><text>")
                    .Append(WebUtility.HtmlEncode(chapter.Title))
                    .Append("</text></navLabel>\n")
                    .Append("      <content src=\"chapters/")
                    .Append(chapter.FileName)
                    .Append("\" />\n")
                    .Append("    </navPoint>\n");
            }

            builder.Append("  </navMap>\n</ncx>\n");
            return builder.ToString();
        }

        private static string BuildOpf(
            IReadOnlyList<EpubChapter> chapters,
            string title,
            string author,
            string language,
            string identifier,
            string modified,
            EpubAsset? coverAsset)
        {
            StringBuilder manifest = new();
            StringBuilder spine = new();

            manifest.Append("    <item id=\"nav\" href=\"nav.xhtml\" media-type=\"application/xhtml+xml\" properties=\"nav\" />\n")
                .Append("    <item id=\"toc\" href=\"toc.ncx\" media-type=\"application/x-dtbncx+xml\" />\n")
                .Append("    <item id=\"css\" href=\"styles/style.css\" media-type=\"text/css\" />\n");

            if (coverAsset is not null)
            {
                manifest.Append("    <item id=\"cover-image\" href=\"images/")
                    .Append(coverAsset.FileName)
                    .Append("\" media-type=\"")
                    .Append(coverAsset.MediaType)
                    .Append("\" properties=\"cover-image\" />\n");
            }

            foreach (EpubChapter chapter in chapters)
            {
                manifest.Append("    <item id=\"")
                    .Append(chapter.Id)
                    .Append("\" href=\"chapters/")
                    .Append(chapter.FileName)
                    .Append("\" media-type=\"application/xhtml+xml\" />\n");
                spine.Append("    <itemref idref=\"").Append(chapter.Id).Append("\" />\n");
            }

            StringBuilder builder = new();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n")
                .Append("<package version=\"3.0\" xmlns=\"http://www.idpf.org/2007/opf\" unique-identifier=\"bookid\">\n")
                .Append("  <metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\">\n")
                .Append($"    <dc:identifier id=\"bookid\">{WebUtility.HtmlEncode(identifier)}</dc:identifier>\n")
                .Append($"    <dc:title>{WebUtility.HtmlEncode(title)}</dc:title>\n")
                .Append($"    <dc:creator>{WebUtility.HtmlEncode(author)}</dc:creator>\n")
                .Append($"    <dc:language>{WebUtility.HtmlEncode(language)}</dc:language>\n")
                .Append($"    <meta property=\"dcterms:modified\">{WebUtility.HtmlEncode(modified)}</meta>\n")
                .Append("  </metadata>\n")
                .Append("  <manifest>\n")
                .Append(manifest)
                .Append("  </manifest>\n")
                .Append("  <spine toc=\"toc\">\n")
                .Append(spine)
                .Append("  </spine>\n")
                .Append("</package>\n");

            return builder.ToString();
        }

        private static string BuildStylesheet()
        {
            return "body { font-family: serif; line-height: 1.6; margin: 1.2em; }\n" +
                   "h1, h2, h3 { margin-top: 1.5em; }\n" +
                   "p { margin: 0.8em 0; }\n" +
                   "ul, ol { margin: 0.8em 0 0.8em 1.2em; }\n" +
                   ".book-cover-page { min-height: 100vh; display: flex; align-items: center; justify-content: center; margin: 0; }\n" +
                   ".book-cover-page img { max-width: 100%; max-height: 95vh; display: block; margin: 0 auto; }\n";
        }

        private static void AddTextEntry(ZipArchive archive, string path, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using Stream stream = entry.Open();
            using StreamWriter writer = new(stream, new UTF8Encoding(false));
            writer.Write(content);
        }

        private static void AddBinaryEntry(ZipArchive archive, string path, byte[] content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using Stream stream = entry.Open();
            stream.Write(content, 0, content.Length);
        }

        private static EpubAsset? TryBuildCoverAsset(ExportOptions options)
        {
            if (!ExportHelpers.ShouldIncludeCover(options))
            {
                return null;
            }

            return TryParseDataUriAsset(options.CoverImageUrl!, "cover");
        }

        private static EpubAsset? TryParseDataUriAsset(string dataUri, string fileBaseName)
        {
            int commaIndex = dataUri.IndexOf(',');
            if (commaIndex <= 0 || !dataUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string header = dataUri.Substring(5, commaIndex - 5);
            string payload = dataUri[(commaIndex + 1)..];
            if (!header.Contains("base64", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string mediaType = header.Split(';', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            string extension = mediaType.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                _ => string.Empty
            };

            if (string.IsNullOrWhiteSpace(extension))
            {
                return null;
            }

            return new EpubAsset(
                fileBaseName + extension,
                mediaType,
                Convert.FromBase64String(payload));
        }

        private sealed record EpubChapter(string FileName, string Title, string BodyHtml, string Id);
        private sealed record EpubAsset(string FileName, string MediaType, byte[] Bytes);
    }
}
