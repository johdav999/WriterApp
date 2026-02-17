using System;
using WriterApp.Application.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class QualityFixClientHelpersTests
    {
        [Theory]
        [InlineData("append", true)]
        [InlineData("Append", true)]
        [InlineData("replace", false)]
        [InlineData("", false)]
        public void IsAppendMode_ParsesMode(string mode, bool expected)
        {
            Assert.Equal(expected, QualityFixClientHelpers.IsAppendMode(mode));
        }

        [Fact]
        public void MergeImportedHtmlForAppend_AppendsWithBoundary()
        {
            string merged = QualityFixClientHelpers.MergeImportedHtmlForAppend(
                "<p>Existing text.</p>",
                "<p>Imported text.</p>");

            Assert.Contains("<p>Existing text.</p>", merged);
            Assert.Contains("<p><br /></p>", merged);
            Assert.EndsWith("<p>Imported text.</p>", merged, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildProposalAfterText_DeleteFix_ShowsRemoved()
        {
            QualityIssueFixDto fix = new(
                Kind: "delete",
                From: 1,
                To: 5,
                Text: "irrelevant-meta");

            string after = QualityFixClientHelpers.BuildProposalAfterText(fix);

            Assert.Equal("(removed)", after);
        }

        [Fact]
        public void BuildProposalAfterText_MetaLeak_SuppressesText()
        {
            QualityIssueFixDto fix = new(
                Kind: "replace",
                From: 1,
                To: 5,
                Text: "{\"model\":\"gpt-4.1\",\"input\":\"rewrite this\"}");

            string after = QualityFixClientHelpers.BuildProposalAfterText(fix);

            Assert.Equal(string.Empty, after);
        }

        [Fact]
        public void SanitizeUiLabel_RemovesInvalidGlyphsAndControls()
        {
            string sanitized = QualityFixClientHelpers.SanitizeUiLabel("16/02/2026 06:32 \uFFFD Snapshot \u0007 (Pre-AI)");

            Assert.Equal("16/02/2026 06:32 Snapshot (Pre-AI)", sanitized);
        }
    }
}
