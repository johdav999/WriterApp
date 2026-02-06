using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.Application.Exporting;
using WriterApp.Domain.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class EpubExportTests
    {
        [Fact]
        public void EpubExport_BuildsZipWithRequiredEntries()
        {
            Document document = BuildDocument("<h1>Chapter One</h1><p>Hello EPUB.</p>");
            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Epub,
                new ExportOptions(IncludeTitlePage: false, ChapterBreakRules: new[] { "h1" }),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using ZipArchive archive = new(stream, ZipArchiveMode.Read);

            ZipArchiveEntry mimetype = archive.Entries.FirstOrDefault(e => e.FullName == "mimetype")
                ?? throw new InvalidOperationException("Missing mimetype entry.");
            Assert.Equal(mimetype.Length, mimetype.CompressedLength);

            Assert.Contains(archive.Entries, e => e.FullName == "META-INF/container.xml");
            Assert.Contains(archive.Entries, e => e.FullName == "OEBPS/content.opf");
            Assert.Contains(archive.Entries, e => e.FullName == "OEBPS/nav.xhtml");
            Assert.Contains(archive.Entries, e => e.FullName == "OEBPS/toc.ncx");
            Assert.Contains(archive.Entries, e => e.FullName.StartsWith("OEBPS/chapters/", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void EpubExport_OpfReferencesChapters()
        {
            Document document = BuildDocument("<h1>Chapter One</h1><p>Hello EPUB.</p>");
            ExportService service = BuildExportService();
            ExportResult result = service.ExportAsync(
                document,
                ExportKind.Document,
                ExportFormat.Epub,
                new ExportOptions(IncludeTitlePage: false, ChapterBreakRules: new[] { "h1" }),
                "user",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            using MemoryStream stream = new(result.Content);
            using ZipArchive archive = new(stream, ZipArchiveMode.Read);
            ZipArchiveEntry opfEntry = archive.GetEntry("OEBPS/content.opf") ?? throw new InvalidOperationException("Missing OPF.");
            string opf = ReadEntry(opfEntry);
            Assert.Contains("chapters/chapter-001.xhtml", opf);
            Assert.Contains("<spine", opf);
        }

        private static string ReadEntry(ZipArchiveEntry entry)
        {
            using Stream stream = entry.Open();
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }

        private static ExportService BuildExportService()
        {
            IExportRenderer[] renderers =
            {
                new EpubExportRenderer()
            };
            return new ExportService(renderers, new StubExportTemplateResolver());
        }

        private static Document BuildDocument(string html)
        {
            return new Document
            {
                DocumentId = Guid.NewGuid(),
                Metadata = new DocumentMetadata
                {
                    Title = "Epub Sample",
                    Language = "en",
                    Author = "Writer"
                },
                Synopsis = new Synopsis { ModifiedUtc = DateTime.UtcNow },
                Chapters =
                {
                    new Chapter
                    {
                        Order = 0,
                        Title = "Epub Sample",
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
                throw new InvalidOperationException("Templates are not used for EPUB exports.");
            }
        }
    }
}
