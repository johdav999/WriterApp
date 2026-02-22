using System;

namespace WriterApp.Application.Documents
{
    public static class QualityIssueCapabilities
    {
        public static bool IsAutoProposable(PageQualityIssueDto issue)
        {
            if (issue is null)
            {
                return false;
            }

            return IsRepeatedWordIssue(issue) || IsSentenceLengthIssue(issue);
        }

        public static bool IsRepeatedWordIssue(PageQualityIssueDto issue)
        {
            string ruleId = issue.RuleId?.Trim() ?? string.Empty;
            string kind = issue.Kind?.Trim() ?? string.Empty;

            return string.Equals(ruleId, "style.repeated_words", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "repeated-word", StringComparison.OrdinalIgnoreCase)
                || kind.Contains("repeated-word", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSentenceLengthIssue(PageQualityIssueDto issue)
        {
            string ruleId = issue.RuleId?.Trim() ?? string.Empty;
            string kind = issue.Kind?.Trim() ?? string.Empty;

            return string.Equals(ruleId, "readability.sentence_length", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ruleId, "style.sentence_length", StringComparison.OrdinalIgnoreCase)
                || kind.Contains("sentence-length", StringComparison.OrdinalIgnoreCase);
        }
    }
}
