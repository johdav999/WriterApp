using WriterApp.Application.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class QualityRewriteOutputValidatorTests
    {
        [Fact]
        public void NormalizeRepeatedWordCandidate_RejectsEmptyQuotedString()
        {
            string candidate = QualityRewriteOutputValidator.NormalizeRepeatedWordCandidate("\"\"");
            Assert.Equal(string.Empty, candidate);
        }

        [Fact]
        public void NormalizeRepeatedWordCandidate_RejectsBulletListOutput()
        {
            string candidate = QualityRewriteOutputValidator.NormalizeRepeatedWordCandidate("- item one\n- item two");
            Assert.Equal(string.Empty, candidate);
        }

        [Fact]
        public void NormalizeRepeatedWordCandidate_ExtractsDelimitedRevisedText()
        {
            string candidate = QualityRewriteOutputValidator.NormalizeRepeatedWordCandidate("<<REVISED>>Hej varlden<<END>>");
            Assert.Equal("Hej varlden", candidate);
        }

        [Fact]
        public void NormalizeRepeatedWordCandidate_ExtractsJsonRevisedText()
        {
            string candidate = QualityRewriteOutputValidator.NormalizeRepeatedWordCandidate("{\"revisedText\":\"Hej varlden\"}");
            Assert.Equal("Hej varlden", candidate);
        }

        [Fact]
        public void NormalizeRepeatedWordCandidate_AcceptsLatinScriptProse()
        {
            string input = "Maya checked the clock at 08:20 and realized she could still make it.";
            string candidate = QualityRewriteOutputValidator.NormalizeRepeatedWordCandidate(input);
            Assert.Equal(input, candidate);
        }

        [Fact]
        public void NormalizeRepeatedWordCandidate_AcceptsCjkScript()
        {
            string input = "\u5979\u770b\u4e86\u770b\u65f6\u95f4\uff0c\u786e\u8ba4\u81ea\u5df1\u8fd8\u80fd\u8d76\u4e0a\u4f1a\u8bae\u3002";
            string candidate = QualityRewriteOutputValidator.NormalizeRepeatedWordCandidate(input);
            Assert.Equal(input, candidate);
        }

        [Fact]
        public void NormalizeRepeatedWordCandidate_AcceptsRtlScript()
        {
            string input = "\u0646\u0638\u0631\u062a \u0625\u0644\u0649 \u0627\u0644\u0633\u0627\u0639\u0629 \u0648\u062a\u0623\u0643\u062f\u062a \u0623\u0646\u0647\u0627 \u0645\u0627 \u0632\u0627\u0644\u062a \u0633\u062a\u0635\u0644 \u0641\u064a \u0627\u0644\u0645\u0648\u0639\u062f.";
            string candidate = QualityRewriteOutputValidator.NormalizeRepeatedWordCandidate(input);
            Assert.Equal(input, candidate);
        }

        [Fact]
        public void TryValidateRepeatedWordReduction_RejectsUnchangedRepetition()
        {
            const string original = "Maya looked at the clock and the clock made her anxious.";
            const string candidate = "Maya looked at the clock and the clock made her anxious.";

            bool ok = QualityRewriteOutputValidator.TryValidateRepeatedWordReduction(
                original,
                candidate,
                "clock",
                out int originalCount,
                out int candidateCount,
                out string? reason);

            Assert.False(ok);
            Assert.Equal(2, originalCount);
            Assert.Equal(2, candidateCount);
            Assert.Equal("repetition_not_reduced", reason);
        }

        [Fact]
        public void TryValidateRepeatedWordReduction_AcceptsReducedRepetition()
        {
            const string original = "Maya looked at the clock and the clock made her anxious.";
            const string candidate = "Maya looked at the clock, and the ticking made her anxious.";

            bool ok = QualityRewriteOutputValidator.TryValidateRepeatedWordReduction(
                original,
                candidate,
                "clock",
                out int originalCount,
                out int candidateCount,
                out string? reason);

            Assert.True(ok);
            Assert.Equal(2, originalCount);
            Assert.Equal(1, candidateCount);
            Assert.Null(reason);
        }

        [Fact]
        public void TryValidateRepeatedWordReduction_RejectsUnchangedRepetition_Cjk()
        {
            const string original = "明日、彼女は明日に備えて早く寝た。";
            const string candidate = "明日、彼女は明日に備えて早く寝た。";

            bool ok = QualityRewriteOutputValidator.TryValidateRepeatedWordReduction(
                original,
                candidate,
                "明日",
                out int originalCount,
                out int candidateCount,
                out string? reason);

            Assert.False(ok);
            Assert.Equal(2, originalCount);
            Assert.Equal(2, candidateCount);
            Assert.Equal("repetition_not_reduced", reason);
        }

        [Fact]
        public void TryValidateRepeatedWordReduction_AcceptsReducedRepetition_Rtl()
        {
            const string original = "هي تعرف أن الوقت ضيق وأن الوقت مهم.";
            const string candidate = "هي تعرف أن الوقت ضيق وأن الأمر مهم.";

            bool ok = QualityRewriteOutputValidator.TryValidateRepeatedWordReduction(
                original,
                candidate,
                "الوقت",
                out int originalCount,
                out int candidateCount,
                out string? reason);

            Assert.True(ok);
            Assert.Equal(2, originalCount);
            Assert.Equal(1, candidateCount);
            Assert.Null(reason);
        }

        [Fact]
        public void TryValidateRepeatedWordReduction_AllowsEqualCountForShortAnchor()
        {
            const string original = "ha ha ha";
            const string candidate = "ha ha ha";

            bool ok = QualityRewriteOutputValidator.TryValidateRepeatedWordReduction(
                original,
                candidate,
                "ha",
                out int originalCount,
                out int candidateCount,
                out string? reason);

            Assert.True(ok);
            Assert.Equal(3, originalCount);
            Assert.Equal(3, candidateCount);
            Assert.Null(reason);
        }

        [Fact]
        public void TryValidateRepeatedWordReduction_RejectsIncreaseForShortAnchor()
        {
            const string original = "ha ha ha";
            const string candidate = "ha ha ha ha";

            bool ok = QualityRewriteOutputValidator.TryValidateRepeatedWordReduction(
                original,
                candidate,
                "ha",
                out int originalCount,
                out int candidateCount,
                out string? reason);

            Assert.False(ok);
            Assert.Equal(3, originalCount);
            Assert.Equal(4, candidateCount);
            Assert.Equal("repetition_increased", reason);
        }

        [Fact]
        public void CountOccurrences_IgnoresSubstringMatchesForStandaloneWord()
        {
            const string text = "During winter, Lin stepped in and stayed inside.";

            int count = QualityRewriteOutputValidator.CountOccurrences(text, "in");

            Assert.Equal(1, count);
        }

        [Fact]
        public void CountOccurrences_NormalizesCaseAndTrailingPunctuation()
        {
            const string text = "Clock, clock. CLOCK!";

            int count = QualityRewriteOutputValidator.CountOccurrences(text, "clock");

            Assert.Equal(3, count);
        }

        [Fact]
        public void TryValidateRepeatedWordReduction_DoesNotTreatSubstringAsRepetition()
        {
            const string original = "During the briefing, Lin stepped in quietly.";
            const string candidate = "During the briefing, Lin slipped inside quietly.";

            bool ok = QualityRewriteOutputValidator.TryValidateRepeatedWordReduction(
                original,
                candidate,
                "in",
                out int originalCount,
                out int candidateCount,
                out string? reason);

            Assert.True(ok);
            Assert.Equal(1, originalCount);
            Assert.Equal(0, candidateCount);
            Assert.Null(reason);
        }
    }
}
