using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WriterApp.Application.Importing;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class SectionImportServiceTests
    {
        [Fact]
        public async Task ConvertAsync_Txt_ProducesParagraphHtml()
        {
            SectionImportService service = new();
            byte[] bytes = Encoding.UTF8.GetBytes("First line.\n\nSecond line.");

            SectionImportResult result = await service.ConvertAsync(
                "sample.txt",
                bytes,
                new SectionImportOptions(NormalizeWhitespace: true, PreserveTxtLineBreaks: false),
                CancellationToken.None);

            Assert.Equal("txt", result.Format);
            Assert.Contains("<p>First line.</p>", result.Html);
            Assert.Contains("<p>Second line.</p>", result.Html);
            Assert.True(result.Stats.Paragraphs >= 2);
        }

        [Fact]
        public async Task ConvertAsync_Docx_PreservesHeadingAndInlineFormatting()
        {
            SectionImportService service = new();
            byte[] bytes = BuildSimpleDocx();

            SectionImportResult result = await service.ConvertAsync(
                "scene.docx",
                bytes,
                new SectionImportOptions(NormalizeWhitespace: true, PreserveTxtLineBreaks: false),
                CancellationToken.None);

            Assert.Equal("docx", result.Format);
            Assert.Contains("<h1>Chapter One</h1>", result.Html);
            Assert.Contains("<strong>Bold</strong>", result.Html);
            Assert.Contains("<em>Italic</em>", result.Html);
            Assert.True(result.Stats.Headings >= 1);
        }

        private static byte[] BuildSimpleDocx()
        {
            using MemoryStream stream = new();
            using (WordprocessingDocument doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
            {
                MainDocumentPart main = doc.AddMainDocumentPart();
                main.Document = new Document(new Body());
                StyleDefinitionsPart styles = main.AddNewPart<StyleDefinitionsPart>();
                styles.Styles = new Styles(
                    new Style(
                        new Name { Val = "Heading 1" },
                        new BasedOn { Val = "Normal" },
                        new UIPriority { Val = 9 },
                        new PrimaryStyle(),
                        new StyleParagraphProperties(),
                        new StyleRunProperties())
                    {
                        Type = StyleValues.Paragraph,
                        StyleId = "Heading1",
                        CustomStyle = true
                    });

                Body body = main.Document.Body!;
                body.AppendChild(new Paragraph(
                    new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }),
                    new Run(new Text("Chapter One"))));
                body.AppendChild(new Paragraph(
                    new Run(new RunProperties(new Bold()), new Text("Bold")),
                    new Run(new Text(" and ")),
                    new Run(new RunProperties(new Italic()), new Text("Italic"))));
                main.Document.Save();
            }

            return stream.ToArray();
        }
    }
}
