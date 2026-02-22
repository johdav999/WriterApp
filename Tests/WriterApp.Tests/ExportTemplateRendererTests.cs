using System;
using WriterApp.Application.Exporting;
using WriterApp.Data.Exporting;
using WriterApp.Domain.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class ExportTemplateRendererTests
    {
        [Fact]
        public void HtmlRenderer_ReplacesHeaderFooterTokens()
        {
            Document document = BuildDocument("Tokenized Draft");
            ExportTemplate template = ExportTemplateDefaults.CreateManuscript("user", DateTimeOffset.UtcNow);
            template.HeaderEnabled = true;
            template.HeaderLeft = "{DocumentTitle} {Date}";
            template.FooterEnabled = true;
            template.FooterRight = "{PageNumber}/{TotalPages}";
            template.PageNumbersEnabled = true;

            ExportOptions options = new(IncludeTitlePage: true, TemplateId: template.Id, Template: template);
            TemplatedHtmlExportRenderer renderer = new();

            string html = renderer.RenderBodyHtml(document, options);

            Assert.Contains("Tokenized Draft", html);
            string today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
            Assert.Contains(today, html);
            Assert.Contains("counter(page)", html);
            Assert.Contains("\"?\"", html);
        }

        [Fact]
        public void HtmlRenderer_GeneratesTocForHeadings()
        {
            Document document = BuildDocument("TOC Draft");
            document.Chapters[0].Sections[0] = document.Chapters[0].Sections[0] with
            {
                Content = new SectionContent
                {
                    Format = "markdown",
                    Value = "# Heading One\n\n## Subheading A\n\nParagraph text."
                }
            };

            ExportTemplate template = ExportTemplateDefaults.CreateManuscript("user", DateTimeOffset.UtcNow);
            template.TocEnabled = true;
            template.TocDepth = 2;

            ExportOptions options = new(IncludeTitlePage: true, TemplateId: template.Id, Template: template);
            TemplatedHtmlExportRenderer renderer = new();

            string html = renderer.RenderBodyHtml(document, options);

            Assert.Contains("export-toc", html);
            Assert.Contains("href=\"#heading-one\"", html);
            Assert.Contains("href=\"#subheading-a\"", html);
        }

        private static Document BuildDocument(string title)
        {
            Document document = new()
            {
                Metadata = new DocumentMetadata
                {
                    Title = title,
                    Language = "en",
                    CreatedUtc = DateTime.UtcNow,
                    ModifiedUtc = DateTime.UtcNow
                },
                Chapters =
                {
                    new Chapter
                    {
                        Order = 0,
                        Title = title,
                        Sections =
                        {
                            new Section
                            {
                                Order = 0,
                                Title = "Section One",
                                Content = new SectionContent
                                {
                                    Format = "markdown",
                                    Value = "Body"
                                },
                                AI = new SectionAIInfo()
                            }
                        }
                    }
                }
            };

            return document;
        }
    }
}
