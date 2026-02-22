using WriterApp.Application.Continuity;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class ContinuityProposalPreviewBuilderTests
    {
        [Fact]
        public void Build_UsesAnchorRange_ForBeforeAndContext()
        {
            const string text = "Alpha beta gamma delta epsilon zeta.";
            ContinuityProposalPreview preview = ContinuityProposalPreviewBuilder.Build(
                text,
                anchorStart: 6,
                anchorLength: 4,
                suggestedFix: "BETA",
                contextRadius: 5);

            Assert.Equal("beta", preview.Before);
            Assert.Equal("BETA", preview.After);
            Assert.Equal("lpha ", preview.Prefix);
            Assert.Equal(" gamm", preview.Suffix);
            Assert.Equal(6, preview.Start);
            Assert.Equal(4, preview.Length);
        }

        [Fact]
        public void Build_ClampsOutOfRangeAnchors_Deterministically()
        {
            const string text = "short";
            ContinuityProposalPreview preview = ContinuityProposalPreviewBuilder.Build(
                text,
                anchorStart: 999,
                anchorLength: 20,
                suggestedFix: "replacement",
                contextRadius: 20);

            Assert.Equal(string.Empty, preview.Before);
            Assert.Equal("replacement", preview.After);
            Assert.Equal("short", preview.Prefix);
            Assert.Equal(string.Empty, preview.Suffix);
            Assert.Equal(text.Length, preview.Start);
            Assert.Equal(0, preview.Length);
        }

        [Fact]
        public void ExpandToSentenceSpan_AlignsToWholeSentenceAndWordBoundaries()
        {
            const string text = "Prefix. Maya checked her phone at 08:05 and sighed. She sprinted across the lobby.";
            int anchorStart = text.IndexOf("phone", System.StringComparison.Ordinal) + 2;
            ContinuityRewriteSpan span = ContinuityRewriteSpanResolver.ExpandToSentenceSpan(text, anchorStart, 6);

            Assert.Equal("Maya checked her phone at 08:05 and sighed.", span.Before);
            Assert.True(span.StartsSentence);
            Assert.True(span.EndsSentence);
        }

        [Fact]
        public void BuildFromRange_KeepsExactRange()
        {
            const string text = "A. Maya checked her phone and sighed.";
            int start = text.IndexOf("checked", System.StringComparison.Ordinal);
            int length = "checked her phone".Length;

            ContinuityRewriteSpan span = ContinuityRewriteSpanResolver.BuildFromRange(text, start, length);

            Assert.Equal(start, span.Start);
            Assert.Equal(length, span.Length);
            Assert.Equal("checked her phone", span.Before);
        }

        [Fact]
        public void ValidateReplacement_RejectsMidWordJoin()
        {
            bool valid = ContinuityRewriteValidator.ValidateReplacement(
                "late f",
                "Maya sprinted into the room.",
                " The presenter paused.",
                startsSentence: false,
                endsSentence: true,
                beforeLength: 40,
                out string? error);

            Assert.False(valid);
            Assert.Equal("Suggestion starts mid-word against surrounding text.", error);
        }

        [Fact]
        public void ValidateReplacement_RejectsTrailingDuplicateClause()
        {
            bool valid = ContinuityRewriteValidator.ValidateReplacement(
                "Maya checked her phone.",
                "She sprinted across the lobby and slipped into the conference room at 07:50",
                " the conference room at 07:50, just as the presenter began.",
                startsSentence: true,
                endsSentence: false,
                beforeLength: 86,
                out string? error);

            Assert.False(valid);
            Assert.Equal("Suggestion duplicates trailing text from the kept suffix.", error);
        }

        [Fact]
        public void ValidateReplacement_AcceptsCleanSentenceRewrite()
        {
            bool valid = ContinuityRewriteValidator.ValidateReplacement(
                "The meeting started at 08:00. ",
                "Maya sprinted across the lobby and slipped into the conference room at 08:05, just as the presenter began.",
                " By 08:10, she was back at her desk.",
                startsSentence: true,
                endsSentence: true,
                beforeLength: 92,
                out string? error);

            Assert.True(valid);
            Assert.Null(error);
        }
    }
}
