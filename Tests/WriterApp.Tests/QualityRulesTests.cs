using System.Collections.Generic;
using System.Linq;
using WriterApp.Application.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class QualityRulesTests
    {
        [Fact]
        public void RepeatedWordRule_AdjacentDuplicate_ProducesIssueWithoutDeleteFix()
        {
            const string text = "The the lantern flickered.";
            QualityCheckContext context = BuildContext(text);
            RepeatedWordRule rule = new();

            QualityIssue issue = rule.Evaluate(context).First();

            Assert.Equal("style.repeated_words", issue.RuleId);
            Assert.Equal("repeated-word", issue.Kind);
            Assert.Null(issue.Fix);
        }

        [Fact]
        public void RepeatedWordRule_NormalArticlesDoNotBecomeRewriteTargets()
        {
            const string text = "The train reached the platform beside the station. A porter carried a bag and an umbrella past an officer.";
            Assert.Empty(new RepeatedWordRule().Evaluate(BuildContext(text)));
        }

        [Fact]
        public void RepeatedWordRule_KeepsLatestOccurrenceInItsRollingWindow()
        {
            const string text = "clock one two clock three four clock";
            var issues = new RepeatedWordRule().Evaluate(BuildContext(text)).ToArray();
            Assert.Equal(2, issues.Length);
            Assert.All(issues, issue => Assert.Equal("clock", text[issue.StartOffset..issue.EndOffset]));
        }

        [Fact]
        public void PassiveVoiceRule_ProducesIssueWithoutFix()
        {
            const string text = "The gate was opened at dawn.";
            QualityCheckContext context = BuildContext(text);
            PassiveVoiceRule rule = new();

            QualityIssue issue = rule.Evaluate(context).First();

            Assert.Null(issue.Fix);
        }

        [Fact]
        public void ParagraphLengthRule_LongParagraph_ProducesReplaceFix()
        {
            string text = string.Join(
                " ",
                Enumerable.Repeat(
                    "Sara opened the door and looked down the hallway where the lights flickered in the cold draft.",
                    14));

            QualityCheckContext context = BuildContext(text);
            ParagraphLengthRule rule = new();

            QualityIssue issue = rule.Evaluate(context).First();

            Assert.NotNull(issue.Fix);
            Assert.Equal("replace", issue.Fix!.Kind);
            Assert.Equal(issue.StartOffset, issue.Fix.From);
            Assert.Equal(issue.EndOffset, issue.Fix.To);
            Assert.Contains("\n\n", issue.Fix.Text);
        }

        private static QualityCheckContext BuildContext(string text)
        {
            IReadOnlyList<QualityToken> tokens = QualityTextAnalyzer.GetTokens(text);
            IReadOnlyList<QualitySentence> sentences = QualityTextAnalyzer.GetSentences(text);
            IReadOnlyList<QualityParagraph> paragraphs = QualityTextAnalyzer.GetParagraphs(text);
            return new QualityCheckContext(text, tokens, sentences, paragraphs, new List<string>());
        }
    }
}
