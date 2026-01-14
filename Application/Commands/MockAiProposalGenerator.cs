using System;

namespace WriterApp.Application.Commands
{
    public sealed class MockAiProposalGenerator : IAiProposalGenerator
    {
        public string Generate(AiActionKind kind, string instruction, string originalText)
        {
            string trimmed = originalText ?? string.Empty;
            return kind switch
            {
                AiActionKind.RewriteSelection => $"[AI rewrite] {trimmed}",
                AiActionKind.ShortenSelection => $"[AI shorten] {TrimToLength(trimmed, 60)}",
                AiActionKind.FixGrammar => $"[AI grammar fix] {trimmed}",
                AiActionKind.ChangeTone => $"[AI tone shift] {trimmed}",
                AiActionKind.SummarizeParagraph => $"[AI summary] {TrimToLength(trimmed, 80)}",
                _ => $"[AI] {trimmed}"
            };
        }

        private static string TrimToLength(string text, int length)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (text.Length <= length)
            {
                return text;
            }

            return text.Substring(0, length).TrimEnd() + "...";
        }
    }
}
