using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Application.State;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents
{
    public sealed class PageVersionService : IPageVersionService
    {
        private readonly AppDbContext _dbContext;
        private readonly IEntitlementService _entitlementService;
        private readonly ILogger<PageVersionService> _logger;

        public PageVersionService(
            AppDbContext dbContext,
            IEntitlementService entitlementService,
            ILogger<PageVersionService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PageVersionRecord?> CreateSnapshotAsync(
            string userId,
            PageRecord page,
            string content,
            string reason,
            bool allowDuplicate,
            CancellationToken ct)
        {
            if (!await _entitlementService.HasAsync(userId, "history.enabled"))
            {
                return null;
            }

            string normalizedContent = content ?? string.Empty;
            string hash = ComputeHash(normalizedContent);
            if (!allowDuplicate)
            {
                PageVersionRecord? latest = await GetLatestVersionAsync(page.Id, ct);
                if (latest is not null && string.Equals(latest.ContentTextHash, hash, StringComparison.Ordinal))
                {
                    _logger.LogInformation(
                        "PageVersion snapshot skipped: duplicate content PageId={PageId} Reason={Reason}",
                        page.Id,
                        reason ?? "autosnap");
                    return null;
                }
            }

            byte[] compressed = Compress(normalizedContent);
            string plain = PlainTextMapper.ToPlainText(normalizedContent);
            int wordCount = CountWords(plain);

            PageVersionRecord version = new()
            {
                Id = Guid.NewGuid(),
                PageId = page.Id,
                DocumentId = page.DocumentId,
                CreatedAt = DateTimeOffset.UtcNow,
                Reason = reason ?? "autosnap",
                ContentCompressed = compressed,
                ContentTextHash = hash,
                SizeBytes = compressed.Length,
                WordCount = wordCount
            };

            _dbContext.PageVersions.Add(version);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation(
                "PageVersion saved PageId={PageId} DocumentId={DocumentId} Reason={Reason} SizeBytes={SizeBytes} WordCount={WordCount}",
                version.PageId,
                version.DocumentId,
                version.Reason,
                version.SizeBytes,
                version.WordCount);

            await CleanupAsync(userId, page.Id, ct);
            return version;
        }

        public async Task<PageVersionRecord?> CreateAutosnapshotIfDueAsync(
            string userId,
            PageRecord page,
            string content,
            TimeSpan minAge,
            CancellationToken ct)
        {
            if (!await _entitlementService.HasAsync(userId, "history.enabled"))
            {
                _logger.LogInformation(
                    "PageVersion autosnap skipped: history disabled UserId={UserId} PageId={PageId}",
                    userId,
                    page.Id);
                return null;
            }

            PageVersionRecord? latest = await GetLatestVersionAsync(page.Id, ct);
            if (latest is not null)
            {
                TimeSpan age = DateTimeOffset.UtcNow - latest.CreatedAt;
                if (age < minAge)
                {
                    _logger.LogInformation(
                        "PageVersion autosnap skipped: minAge not met PageId={PageId} AgeSeconds={AgeSeconds} MinSeconds={MinSeconds}",
                        page.Id,
                        Math.Round(age.TotalSeconds, 2),
                        Math.Round(minAge.TotalSeconds, 2));
                    return null;
                }

                string hash = ComputeHash(content ?? string.Empty);
                if (string.Equals(latest.ContentTextHash, hash, StringComparison.Ordinal))
                {
                    _logger.LogInformation(
                        "PageVersion autosnap skipped: content unchanged PageId={PageId}",
                        page.Id);
                    return null;
                }
            }

            return await CreateSnapshotAsync(userId, page, content, "autosnap", allowDuplicate: false, ct);
        }

        public async Task<IReadOnlyList<PageVersionRecord>> ListVersionsAsync(
            string userId,
            Guid pageId,
            CancellationToken ct)
        {
            List<PageVersionRecord> versions = await _dbContext.PageVersions
                .AsNoTracking()
                .Join(_dbContext.Documents.AsNoTracking(),
                    version => version.DocumentId,
                    document => document.Id,
                    (version, document) => new { version, document })
                .Where(pair => pair.version.PageId == pageId && pair.document.OwnerUserId == userId)
                .Select(pair => pair.version)
                .ToListAsync(ct);

            // SQLite doesn't support ORDER BY on DateTimeOffset, so order in-memory.
            return versions
                .OrderByDescending(version => version.CreatedAt)
                .ToList();
        }

        public async Task<PageVersionRecord?> GetVersionAsync(
            string userId,
            Guid versionId,
            CancellationToken ct)
        {
            return await _dbContext.PageVersions
                .Join(_dbContext.Documents,
                    version => version.DocumentId,
                    document => document.Id,
                    (version, document) => new { version, document })
                .Where(pair => pair.version.Id == versionId && pair.document.OwnerUserId == userId)
                .Select(pair => pair.version)
                .FirstOrDefaultAsync(ct);
        }

        public string DecompressContent(PageVersionRecord version)
        {
            if (version.ContentCompressed is null || version.ContentCompressed.Length == 0)
            {
                return string.Empty;
            }

            using MemoryStream input = new(version.ContentCompressed);
            using GZipStream gzip = new(input, CompressionMode.Decompress);
            using MemoryStream output = new();
            gzip.CopyTo(output);
            return Encoding.UTF8.GetString(output.ToArray());
        }

        public async Task CleanupAsync(string userId, Guid pageId, CancellationToken ct)
        {
            int? maxVersions = await _entitlementService.GetIntAsync(userId, "history.max_versions");
            int? retentionDays = await _entitlementService.GetIntAsync(userId, "history.retention_days");

            if (maxVersions is null && retentionDays is null)
            {
                return;
            }

            bool deleted = false;
            DateTimeOffset now = DateTimeOffset.UtcNow;

            if (retentionDays.HasValue && retentionDays.Value > 0)
            {
                DateTimeOffset cutoff = now.AddDays(-retentionDays.Value);
                List<PageVersionRecord> expired = await _dbContext.PageVersions
                    .Where(version => version.PageId == pageId && version.CreatedAt < cutoff)
                    .ToListAsync(ct);
                if (expired.Count > 0)
                {
                    _dbContext.PageVersions.RemoveRange(expired);
                    deleted = true;
                }
            }

            if (maxVersions.HasValue && maxVersions.Value > 0)
            {
                List<PageVersionRecord> pageVersions = await _dbContext.PageVersions
                    .Where(version => version.PageId == pageId)
                    .ToListAsync(ct);

                List<Guid> overflowIds = pageVersions
                    .OrderByDescending(version => version.CreatedAt)
                    .Skip(maxVersions.Value)
                    .Select(version => version.Id)
                    .ToList();
                if (overflowIds.Count > 0)
                {
                    List<PageVersionRecord> overflow = await _dbContext.PageVersions
                        .Where(version => overflowIds.Contains(version.Id))
                        .ToListAsync(ct);
                    if (overflow.Count > 0)
                    {
                        _dbContext.PageVersions.RemoveRange(overflow);
                        deleted = true;
                    }
                }
            }

            if (deleted)
            {
                await _dbContext.SaveChangesAsync(ct);
            }
        }

        private async Task<PageVersionRecord?> GetLatestVersionAsync(Guid pageId, CancellationToken ct)
        {
            List<PageVersionRecord> versions = await _dbContext.PageVersions
                .Where(version => version.PageId == pageId)
                .ToListAsync(ct);

            return versions
                .OrderByDescending(version => version.CreatedAt)
                .FirstOrDefault();
        }

        private static byte[] Compress(string content)
        {
            byte[] input = Encoding.UTF8.GetBytes(content ?? string.Empty);
            using MemoryStream output = new();
            using (GZipStream gzip = new(output, CompressionMode.Compress, leaveOpen: true))
            {
                gzip.Write(input, 0, input.Length);
            }
            return output.ToArray();
        }

        private static string ComputeHash(string content)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        private static int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            int count = 0;
            bool inWord = false;
            foreach (char ch in text)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    if (!inWord)
                    {
                        inWord = true;
                        count++;
                    }
                }
                else
                {
                    inWord = false;
                }
            }
            return count;
        }
    }
}
