using System;
using System.Collections.Generic;

namespace WriterApp.Application.Documents
{
    public sealed record QualityIssueFix(
        string Kind,
        int From,
        int To,
        string? Text);

    public sealed record QualityIssue(
        string IssueKey,
        string RuleId,
        string Kind,
        string Severity,
        string Message,
        string? Suggestion,
        string? AnchorText,
        int StartOffset,
        int EndOffset,
        QualityIssueFix? Fix);

    public sealed record QualityToken(string Text, int Start, int End);

    public sealed record QualitySentence(string Text, int Start, int End, int WordCount);

    public sealed record QualityParagraph(string Text, int Start, int End, int WordCount);

    public sealed class QualityCheckContext
    {
        public QualityCheckContext(
            string text,
            IReadOnlyList<QualityToken> tokens,
            IReadOnlyList<QualitySentence> sentences,
            IReadOnlyList<QualityParagraph> paragraphs,
            IReadOnlyList<string> glossaryTerms)
        {
            Text = text;
            Tokens = tokens;
            Sentences = sentences;
            Paragraphs = paragraphs;
            GlossaryTerms = glossaryTerms;
        }

        public string Text { get; }
        public IReadOnlyList<QualityToken> Tokens { get; }
        public IReadOnlyList<QualitySentence> Sentences { get; }
        public IReadOnlyList<QualityParagraph> Paragraphs { get; }
        public IReadOnlyList<string> GlossaryTerms { get; }
    }

    public interface IQualityRule
    {
        string Id { get; }
        IEnumerable<QualityIssue> Evaluate(QualityCheckContext context);
    }
}
