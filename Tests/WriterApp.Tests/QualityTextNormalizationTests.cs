using System.Collections.Generic;
using System.Linq;
using WriterApp.Application.Documents;
using WriterApp.Application.State;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class QualityTextNormalizationTests
    {
        [Fact]
        public void ToPlainText_TipTapParagraphs_UseDoubleNewlineBoundaries()
        {
            const string html = "<p>First paragraph.</p><p>Second paragraph.</p><p>Third paragraph.</p>";

            string plain = PlainTextMapper.ToPlainText(html);

            Assert.Equal("First paragraph.\n\nSecond paragraph.\n\nThird paragraph.", plain);
            Assert.Equal(2, CountOccurrences(plain, "\n\n"));

            IReadOnlyList<QualityParagraph> paragraphs = QualityTextAnalyzer.GetParagraphs(plain);
            Assert.Equal(3, paragraphs.Count);
            Assert.Equal("First paragraph.", paragraphs[0].Text);
            Assert.Equal("Second paragraph.", paragraphs[1].Text);
            Assert.Equal("Third paragraph.", paragraphs[2].Text);
        }

        [Fact]
        public void ParagraphLengthRule_TriggersOnlyForSingleLongParagraph()
        {
            ParagraphLengthRule rule = new();

            string twoShortParagraphs = $"{BuildWords(80)}\n\n{BuildWords(80)}";
            QualityCheckContext shortContext = BuildContext(twoShortParagraphs);
            Assert.Empty(rule.Evaluate(shortContext));

            string oneLongParagraph = BuildWords(121);
            QualityCheckContext longContext = BuildContext(oneLongParagraph);
            QualityIssue longIssue = Assert.Single(rule.Evaluate(longContext));
            Assert.Equal("paragraph-length", longIssue.Kind);
        }

        [Fact]
        public void ToPlainText_BlockBoundaries_AreDeterministicForOffsets()
        {
            const string html = "<h2>Title</h2><p>Body</p><blockquote>Quote</blockquote><ul><li>A</li><li>B</li></ul>";

            string plain = PlainTextMapper.ToPlainText(html);

            Assert.Equal("Title\n\nBody\n\nQuote\n\nA\n\nB", plain);
            Assert.Equal(24, plain.Length);

            IReadOnlyList<QualityParagraph> paragraphs = QualityTextAnalyzer.GetParagraphs(plain);
            Assert.Equal(5, paragraphs.Count);
            Assert.Equal(7, paragraphs[1].Start);
            Assert.Equal(13, paragraphs[2].Start);
        }

        private static QualityCheckContext BuildContext(string text)
        {
            IReadOnlyList<QualityToken> tokens = QualityTextAnalyzer.GetTokens(text);
            IReadOnlyList<QualitySentence> sentences = QualityTextAnalyzer.GetSentences(text);
            IReadOnlyList<QualityParagraph> paragraphs = QualityTextAnalyzer.GetParagraphs(text);
            return new QualityCheckContext(text, tokens, sentences, paragraphs, new List<string>());
        }

        private static string BuildWords(int count)
        {
            return string.Join(" ", Enumerable.Range(1, count).Select(index => $"word{index}"));
        }

        private static int CountOccurrences(string value, string pattern)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(pattern))
            {
                return 0;
            }

            int count = 0;
            int index = 0;
            while (true)
            {
                int next = value.IndexOf(pattern, index, System.StringComparison.Ordinal);
                if (next < 0)
                {
                    return count;
                }

                count++;
                index = next + pattern.Length;
            }
        }
    }
}
