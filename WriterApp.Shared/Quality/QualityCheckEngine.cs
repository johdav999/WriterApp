using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace WriterApp.Application.Documents
{
    public sealed class QualityCheckEngine
    {
        private readonly IReadOnlyList<IQualityRule> _rules;

        public QualityCheckEngine(IEnumerable<IQualityRule> rules)
        {
            _rules = rules?.ToList() ?? new List<IQualityRule>();
        }

        public IReadOnlyList<QualityIssue> Evaluate(QualityCheckContext context, int maxIssues)
        {
            List<QualityIssue> issues = new();
            foreach (IQualityRule rule in _rules)
            {
                foreach (QualityIssue issue in rule.Evaluate(context))
                {
                    if (issues.Count >= maxIssues)
                    {
                        return issues;
                    }

                    string issueKey = BuildIssueKey(rule.Id, issue);
                    issues.Add(issue with { IssueKey = issueKey });
                }
            }

            return issues;
        }

        private static string BuildIssueKey(string ruleId, QualityIssue issue)
        {
            string payload = $"{ruleId}|{issue.StartOffset}|{issue.EndOffset}|{issue.AnchorText}|{issue.Message}";
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
            StringBuilder builder = new(hash.Length * 2);
            foreach (byte b in hash)
            {
                builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
