using System;
using System.Linq;
using WriterApp.Application.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class PageVersionDiffServiceTests
    {
        private readonly IPageVersionDiffService _service = new PageVersionDiffService();

        [Fact]
        public void InsertSentenceInParagraph_ShowsChangedBlockWithInsertion()
        {
            string baseHtml = "<p>Hello world.</p>";
            string compareHtml = "<p>Hello world. Added sentence.</p>";

            PageVersionDiffResultDto result = _service.BuildDiff(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                compareToCurrent: true,
                baseHtml,
                compareHtml,
                "word",
                200);

            PageVersionDiffBlockDto block = Assert.Single(result.Blocks);
            Assert.True(string.Equals("changed", block.Status, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(block.InlineSegments ?? Array.Empty<PageVersionDiffSpanDto>(),
                span => span.Kind.Equals("added", StringComparison.OrdinalIgnoreCase)
                        && span.Text.Contains("Added", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void DeleteWord_ShowsRemovedSegment()
        {
            string baseHtml = "<p>Hello brave world.</p>";
            string compareHtml = "<p>Hello world.</p>";

            PageVersionDiffResultDto result = _service.BuildDiff(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                compareToCurrent: true,
                baseHtml,
                compareHtml,
                "word",
                200);

            PageVersionDiffBlockDto block = Assert.Single(result.Blocks);
            Assert.True(string.Equals("changed", block.Status, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(block.InlineSegments ?? Array.Empty<PageVersionDiffSpanDto>(),
                span => span.Kind.Equals("removed", StringComparison.OrdinalIgnoreCase)
                        && span.Text.Contains("brave", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void RemoveParagraph_ShowsRemovedBlock()
        {
            string baseHtml = "<p>First paragraph.</p><p>Second paragraph.</p>";
            string compareHtml = "<p>First paragraph.</p>";

            PageVersionDiffResultDto result = _service.BuildDiff(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                compareToCurrent: true,
                baseHtml,
                compareHtml,
                "word",
                200);

            Assert.Contains(result.Blocks, block => block.Status.Equals("removed", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void AddParagraph_ShowsAddedBlock()
        {
            string baseHtml = "<p>First paragraph.</p>";
            string compareHtml = "<p>First paragraph.</p><p>Second paragraph.</p>";

            PageVersionDiffResultDto result = _service.BuildDiff(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                compareToCurrent: true,
                baseHtml,
                compareHtml,
                "word",
                200);

            Assert.Contains(result.Blocks, block => block.Status.Equals("added", StringComparison.OrdinalIgnoreCase));
        }
    }
}
