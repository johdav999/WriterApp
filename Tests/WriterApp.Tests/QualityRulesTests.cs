using System.Collections.Generic;
using System.Linq;
using WriterApp.Application.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class QualityRulesTests
    {
        [Fact]
        public void RepeatedWordRule_AdjacentDuplicate_ProducesDeleteFix()
        {
            const string text = "The the lantern flickered.";
            QualityCheckContext context = BuildContext(text);
            RepeatedWordRule rule = new();

            QualityIssue issue = rule.Evaluate(context).First();

            Assert.NotNull(issue.Fix);
            Assert.Equal("delete", issue.Fix!.Kind);
            Assert.True(issue.Fix.To > issue.Fix.From);
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

        private static QualityCheckContext BuildContext(string text)
        {
            IReadOnlyList<QualityToken> tokens = QualityTextAnalyzer.GetTokens(text);
            IReadOnlyList<QualitySentence> sentences = QualityTextAnalyzer.GetSentences(text);
            IReadOnlyList<QualityParagraph> paragraphs = QualityTextAnalyzer.GetParagraphs(text);
            return new QualityCheckContext(text, tokens, sentences, paragraphs, new List<string>());
        }
    }
}
