using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Application.State;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents
{
    public interface IQualityCheckService
    {
        Task<QualityCheckRunResultDto> RunChecksAsync(
            string userId,
            PageRecord page,
            QualityCheckRunRequest request,
            CancellationToken ct);

        Task<IReadOnlyList<PageQualityIssueDto>> ListIssuesAsync(
            string userId,
            Guid pageId,
            bool includeDismissed,
            CancellationToken ct);

        Task DismissIssueAsync(string userId, Guid pageId, string issueKey, CancellationToken ct);
        Task ReopenIssueAsync(string userId, Guid pageId, string issueKey, CancellationToken ct);
    }

    public sealed class QualityCheckService : IQualityCheckService
    {
        private const int MaxIssues = 200;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<QualityCheckService> _logger;
        private readonly QualityCheckEngine _engine;

        public QualityCheckService(AppDbContext dbContext, ILogger<QualityCheckService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _engine = new QualityCheckEngine(new IQualityRule[]
            {
                new SentenceLengthRule(),
                new ParagraphLengthRule(),
                new ReadabilityScoreRule(),
                new RepeatedWordRule(),
                new PassiveVoiceRule(),
                new ProperNameConsistencyRule(),
                new TimelineHintRule(),
                new GlossaryRule()
            });
        }

        public async Task<QualityCheckRunResultDto> RunChecksAsync(
            string userId,
            PageRecord page,
            QualityCheckRunRequest request,
            CancellationToken ct)
        {
            string scope = string.IsNullOrWhiteSpace(request.Scope) ? "page" : request.Scope.Trim().ToLowerInvariant();
            string text = scope == "selection"
                ? request.Text ?? string.Empty
                : PlainTextMapper.ToPlainText(page.Content);

            string contentHash = ComputeHash(text);
            bool fromCache = false;
            List<PageQualityIssueRecord> records = new();

            if (scope == "page" && !request.Force)
            {
                records = await _dbContext.PageQualityIssues
                    .AsNoTracking()
                    .Where(issue => issue.PageId == page.Id && issue.Scope == scope && issue.ContentHash == contentHash)
                    .ToListAsync(ct);
                fromCache = records.Count > 0;
            }

            if (!fromCache)
            {
                IReadOnlyList<string> glossary = await _dbContext.DocumentGlossaryEntries
                    .AsNoTracking()
                    .Where(entry => entry.DocumentId == page.DocumentId)
                    .OrderBy(entry => entry.Term)
                    .Select(entry => entry.Term)
                    .ToListAsync(ct);

                IReadOnlyList<QualityToken> tokens = QualityTextAnalyzer.GetTokens(text);
                IReadOnlyList<QualitySentence> sentences = QualityTextAnalyzer.GetSentences(text);
                IReadOnlyList<QualityParagraph> paragraphs = QualityTextAnalyzer.GetParagraphs(text);
                QualityCheckContext context = new(text, tokens, sentences, paragraphs, glossary);
                IReadOnlyList<QualityIssue> issues = _engine.Evaluate(context, MaxIssues);
                Dictionary<string, QualityIssueFix?> fixesByKey = issues
                    .GroupBy(issue => issue.IssueKey, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First().Fix, StringComparer.Ordinal);

                records = issues.Select(issue => new PageQualityIssueRecord
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = page.DocumentId,
                        PageId = page.Id,
                        Scope = scope,
                        IssueKey = issue.IssueKey,
                        RuleId = issue.RuleId,
                        Kind = issue.Kind,
                        Severity = issue.Severity,
                        Message = issue.Message,
                        Suggestion = issue.Suggestion,
                        AnchorText = issue.AnchorText,
                        StartOffset = issue.StartOffset,
                        EndOffset = issue.EndOffset,
                        ContentHash = contentHash,
                        CreatedAt = DateTimeOffset.UtcNow
                    })
                    .ToList();

                if (scope == "page")
                {
                    List<PageQualityIssueRecord> existing = await _dbContext.PageQualityIssues
                        .Where(issue => issue.PageId == page.Id && issue.Scope == scope)
                        .ToListAsync(ct);
                    if (existing.Count > 0)
                    {
                        _dbContext.PageQualityIssues.RemoveRange(existing);
                    }

                    if (records.Count > 0)
                    {
                        _dbContext.PageQualityIssues.AddRange(records);
                    }

                await _dbContext.SaveChangesAsync(ct);
                }

                List<string> dismissedKeys = await _dbContext.PageQualityIssueDismissals
                    .AsNoTracking()
                    .Where(dismissal => dismissal.PageId == page.Id && dismissal.UserId == userId)
                    .Select(dismissal => dismissal.IssueKey)
                    .ToListAsync(ct);
                HashSet<string> dismissedKeysSet = dismissedKeys.ToHashSet(StringComparer.Ordinal);

                List<PageQualityIssueDto> computedIssues = records
                    .Where(record => !dismissedKeysSet.Contains(record.IssueKey))
                    .OrderBy(record => record.Severity)
                    .ThenBy(record => record.StartOffset)
                    .Select(record =>
                    {
                        QualityIssueFix? fix = null;
                        if (fixesByKey.TryGetValue(record.IssueKey, out QualityIssueFix? ruleFix) && ruleFix is not null)
                        {
                            fix = ruleFix;
                        }
                        else
                        {
                            fix = BuildFixFromRecord(text, record);
                        }

                        return MapToDto(record, fix);
                    })
                    .ToList();

                _logger.LogInformation(
                    "Quality checks run (computed). PageId={PageId}, Scope={Scope}, IssueCount={IssueCount}, FixCount={FixCount}, FromCache={FromCache}",
                    page.Id,
                    scope,
                    computedIssues.Count,
                    computedIssues.Count(item => item.Fix is not null),
                    fromCache);

                return new QualityCheckRunResultDto(
                    page.Id,
                    scope,
                    contentHash,
                    fromCache,
                    computedIssues);
            }

            List<string> dismissed = await _dbContext.PageQualityIssueDismissals
                .AsNoTracking()
                .Where(dismissal => dismissal.PageId == page.Id && dismissal.UserId == userId)
                .Select(dismissal => dismissal.IssueKey)
                .ToListAsync(ct);
            HashSet<string> dismissedSet = dismissed.ToHashSet(StringComparer.Ordinal);

            List<PageQualityIssueDto> resultIssues = records
                .Where(record => !dismissedSet.Contains(record.IssueKey))
                .OrderBy(record => record.Severity)
                .ThenBy(record => record.StartOffset)
                .Select(record => MapToDto(record, BuildFixFromRecord(text, record)))
                .ToList();

            _logger.LogInformation(
                "Quality checks run (cache). PageId={PageId}, Scope={Scope}, IssueCount={IssueCount}, FixCount={FixCount}, FromCache={FromCache}",
                page.Id,
                scope,
                resultIssues.Count,
                resultIssues.Count(item => item.Fix is not null),
                fromCache);

            return new QualityCheckRunResultDto(
                page.Id,
                scope,
                contentHash,
                fromCache,
                resultIssues);
        }

        public async Task<IReadOnlyList<PageQualityIssueDto>> ListIssuesAsync(
            string userId,
            Guid pageId,
            bool includeDismissed,
            CancellationToken ct)
        {
            List<PageQualityIssueRecord> records = await _dbContext.PageQualityIssues
                .AsNoTracking()
                .Where(issue => issue.PageId == pageId)
                .ToListAsync(ct);
            string pageText = PlainTextMapper.ToPlainText(
                await _dbContext.Pages
                    .AsNoTracking()
                    .Where(page => page.Id == pageId)
                    .Select(page => page.Content)
                    .FirstOrDefaultAsync(ct)
                ?? string.Empty);

            if (includeDismissed)
            {
                return records
                    .OrderBy(record => record.Severity)
                    .ThenBy(record => record.StartOffset)
                    .Select(record => MapToDto(record, BuildFixFromRecord(pageText, record)))
                    .ToList();
            }

            List<string> dismissed = await _dbContext.PageQualityIssueDismissals
                .AsNoTracking()
                .Where(dismissal => dismissal.PageId == pageId && dismissal.UserId == userId)
                .Select(dismissal => dismissal.IssueKey)
                .ToListAsync(ct);
            HashSet<string> dismissedSet = dismissed.ToHashSet(StringComparer.Ordinal);

            return records
                .Where(record => !dismissedSet.Contains(record.IssueKey))
                .OrderBy(record => record.Severity)
                .ThenBy(record => record.StartOffset)
                .Select(record => MapToDto(record, BuildFixFromRecord(pageText, record)))
                .ToList();
        }

        public async Task DismissIssueAsync(string userId, Guid pageId, string issueKey, CancellationToken ct)
        {
            PageQualityIssueDismissalRecord? existing = await _dbContext.PageQualityIssueDismissals
                .FindAsync(new object[] { userId, pageId, issueKey }, ct);
            if (existing is not null)
            {
                return;
            }

            PageQualityIssueDismissalRecord dismissal = new()
            {
                UserId = userId,
                PageId = pageId,
                IssueKey = issueKey,
                DismissedAt = DateTimeOffset.UtcNow
            };

            _dbContext.PageQualityIssueDismissals.Add(dismissal);
            await _dbContext.SaveChangesAsync(ct);
        }

        public async Task ReopenIssueAsync(string userId, Guid pageId, string issueKey, CancellationToken ct)
        {
            PageQualityIssueDismissalRecord? existing = await _dbContext.PageQualityIssueDismissals
                .FindAsync(new object[] { userId, pageId, issueKey }, ct);
            if (existing is null)
            {
                return;
            }

            _dbContext.PageQualityIssueDismissals.Remove(existing);
            await _dbContext.SaveChangesAsync(ct);
        }

        private static PageQualityIssueDto MapToDto(PageQualityIssueRecord record, QualityIssueFix? fix)
        {
            return new PageQualityIssueDto(
                record.IssueKey,
                record.DocumentId,
                record.PageId,
                record.RuleId,
                record.Kind,
                record.Severity,
                record.Message,
                record.Suggestion,
                record.AnchorText,
                record.StartOffset,
                record.EndOffset,
                fix is null ? null : new QualityIssueFixDto(fix.Kind, fix.From, fix.To, fix.Text, record.AnchorText, record.IssueKey),
                record.CreatedAt);
        }

        private static QualityIssueFix? BuildFixFromRecord(string plainText, PageQualityIssueRecord record)
        {
            if (string.IsNullOrWhiteSpace(plainText))
            {
                return null;
            }

            int from = Math.Clamp(record.StartOffset, 0, plainText.Length);
            int to = Math.Clamp(record.EndOffset, from, plainText.Length);
            if (to <= from)
            {
                return null;
            }

            if (string.Equals(record.RuleId, "consistency.proper_names", StringComparison.OrdinalIgnoreCase)
                || string.Equals(record.RuleId, "terminology.glossary", StringComparison.OrdinalIgnoreCase))
            {
                string? replacement = TryExtractQuotedSuggestion(record.Suggestion);
                if (!string.IsNullOrWhiteSpace(replacement))
                {
                    return new QualityIssueFix("replace", from, to, replacement);
                }
            }

            if (string.Equals(record.RuleId, "readability.paragraph_length", StringComparison.OrdinalIgnoreCase))
            {
                string? replacement = TrySplitParagraph(plainText, from, to);
                if (!string.IsNullOrWhiteSpace(replacement))
                {
                    return new QualityIssueFix("replace", from, to, replacement);
                }
            }

            return null;
        }

        private static string? TrySplitParagraph(string plainText, int from, int to)
        {
            if (string.IsNullOrWhiteSpace(plainText) || to <= from)
            {
                return null;
            }

            string paragraph = plainText[from..to];
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                return null;
            }

            MatchCollection sentenceBreaks = Regex.Matches(paragraph, @"[.!?](?:[""'\u201d\u2019\)\]])?\s+");
            if (sentenceBreaks.Count == 0)
            {
                return null;
            }

            int target = paragraph.Length / 2;
            int splitIndex = -1;
            int bestDistance = int.MaxValue;
            foreach (Match match in sentenceBreaks)
            {
                int candidate = match.Index + match.Length;
                if (candidate <= 0 || candidate >= paragraph.Length)
                {
                    continue;
                }

                int distance = Math.Abs(target - candidate);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    splitIndex = candidate;
                }
            }

            if (splitIndex <= 0 || splitIndex >= paragraph.Length)
            {
                return null;
            }

            string left = paragraph[..splitIndex].TrimEnd();
            string right = paragraph[splitIndex..].TrimStart();
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return null;
            }

            string replacement = $"{left}\n\n{right}";
            return string.Equals(replacement, paragraph, StringComparison.Ordinal) ? null : replacement;
        }

        private static string? TryExtractQuotedSuggestion(string? suggestion)
        {
            if (string.IsNullOrWhiteSpace(suggestion))
            {
                return null;
            }

            Match match = Regex.Match(suggestion, "\"([^\"]+)\"");
            if (!match.Success || match.Groups.Count < 2)
            {
                return null;
            }

            return match.Groups[1].Value.Trim();
        }

        private static string ComputeHash(string value)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            StringBuilder builder = new(hash.Length * 2);
            foreach (byte b in hash)
            {
                builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
