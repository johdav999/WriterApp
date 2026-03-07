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
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents
{
    public sealed class VersionHistoryService : IVersionHistoryService
    {
        private readonly AppDbContext _dbContext;
        private readonly IVersionHistoryPolicyService _policyService;
        private readonly ILogger<VersionHistoryService> _logger;

        public VersionHistoryService(
            AppDbContext dbContext,
            IVersionHistoryPolicyService policyService,
            ILogger<VersionHistoryService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _policyService = policyService ?? throw new ArgumentNullException(nameof(policyService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PageVersionRecord?> CreateCheckpointAsync(
            string userId,
            PageRecord page,
            string content,
            string reason,
            bool allowDuplicate,
            CancellationToken ct)
        {
            VersionHistoryPolicy policy = await _policyService.GetPolicyAsync(userId);
            if (!policy.Enabled)
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
                        "Version history checkpoint skipped: duplicate content PageId={PageId} Reason={Reason}",
                        page.Id,
                        reason ?? "autosave");
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
                Reason = string.IsNullOrWhiteSpace(reason) ? "autosave" : reason.Trim(),
                ContentCompressed = compressed,
                ContentTextHash = hash,
                SizeBytes = compressed.Length,
                WordCount = wordCount
            };

            _dbContext.PageVersions.Add(version);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Version history checkpoint saved PageId={PageId} DocumentId={DocumentId} Reason={Reason} SizeBytes={SizeBytes} WordCount={WordCount}",
                version.PageId,
                version.DocumentId,
                version.Reason,
                version.SizeBytes,
                version.WordCount);

            await PruneAsync(userId, page.Id, ct);
            return version;
        }

        public async Task<PageVersionRecord?> CreateCheckpointIfDueAsync(
            string userId,
            PageRecord page,
            string content,
            TimeSpan minAge,
            CancellationToken ct)
        {
            VersionHistoryPolicy policy = await _policyService.GetPolicyAsync(userId);
            if (!policy.Enabled)
            {
                _logger.LogInformation(
                    "Version history checkpoint skipped: history disabled UserId={UserId} PageId={PageId}",
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
                        "Version history checkpoint skipped: minAge not met PageId={PageId} AgeSeconds={AgeSeconds} MinSeconds={MinSeconds}",
                        page.Id,
                        Math.Round(age.TotalSeconds, 2),
                        Math.Round(minAge.TotalSeconds, 2));
                    return null;
                }

                string hash = ComputeHash(content ?? string.Empty);
                if (string.Equals(latest.ContentTextHash, hash, StringComparison.Ordinal))
                {
                    _logger.LogInformation(
                        "Version history checkpoint skipped: content unchanged PageId={PageId}",
                        page.Id);
                    return null;
                }
            }

            return await CreateCheckpointAsync(userId, page, content ?? string.Empty, "autosave", allowDuplicate: false, ct);
        }

        public async Task<IReadOnlyList<PageVersionRecord>> ListVersionsAsync(
            string userId,
            Guid pageId,
            CancellationToken ct)
        {
            VersionHistoryPolicy policy = await _policyService.GetPolicyAsync(userId);
            if (!policy.Enabled)
            {
                return Array.Empty<PageVersionRecord>();
            }

            await PruneAsync(userId, pageId, ct);

            List<PageVersionRecord> versions = await _dbContext.PageVersions
                .AsNoTracking()
                .Join(_dbContext.Documents.AsNoTracking(),
                    version => version.DocumentId,
                    document => document.Id,
                    (version, document) => new { version, document })
                .Where(pair => pair.version.PageId == pageId && pair.document.OwnerUserId == userId)
                .Select(pair => pair.version)
                .ToListAsync(ct);

            return versions
                .OrderByDescending(version => version.CreatedAt)
                .ToList();
        }

        public async Task<PageVersionRecord?> GetVersionAsync(
            string userId,
            Guid versionId,
            CancellationToken ct)
        {
            VersionHistoryPolicy policy = await _policyService.GetPolicyAsync(userId);
            if (!policy.Enabled)
            {
                return null;
            }

            PageVersionRecord? version = await _dbContext.PageVersions
                .Join(_dbContext.Documents,
                    version => version.DocumentId,
                    document => document.Id,
                    (version, document) => new { version, document })
                .Where(pair => pair.version.Id == versionId && pair.document.OwnerUserId == userId)
                .Select(pair => pair.version)
                .FirstOrDefaultAsync(ct);
            if (version is null)
            {
                return null;
            }

            await PruneAsync(userId, version.PageId, ct);

            return await _dbContext.PageVersions
                .AsNoTracking()
                .Join(_dbContext.Documents.AsNoTracking(),
                    item => item.DocumentId,
                    document => document.Id,
                    (item, document) => new { item, document })
                .Where(pair => pair.item.Id == versionId && pair.document.OwnerUserId == userId)
                .Select(pair => pair.item)
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

        public async Task PruneAsync(string userId, Guid pageId, CancellationToken ct)
        {
            VersionHistoryPolicy policy = await _policyService.GetPolicyAsync(userId);
            if (!policy.Enabled)
            {
                return;
            }

            int? maxVersions = policy.MaxVersions;
            int? retentionDays = policy.RetentionDays;

            if (maxVersions is null && retentionDays is null)
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            List<PageVersionRecord> pageVersions = await _dbContext.PageVersions
                .Where(version => version.PageId == pageId)
                .OrderByDescending(version => version.CreatedAt)
                .ThenByDescending(version => version.Id)
                .ToListAsync(ct);
            if (pageVersions.Count == 0)
            {
                return;
            }

            HashSet<Guid> deleteIds = new();

            if (retentionDays.HasValue && retentionDays.Value > 0)
            {
                DateTimeOffset cutoff = now.AddDays(-retentionDays.Value);
                foreach (PageVersionRecord version in pageVersions)
                {
                    if (version.CreatedAt < cutoff)
                    {
                        deleteIds.Add(version.Id);
                    }
                }
            }

            if (maxVersions.HasValue && maxVersions.Value > 0)
            {
                foreach (PageVersionRecord overflow in pageVersions
                    .Where(version => !deleteIds.Contains(version.Id))
                    .Skip(maxVersions.Value)
                    .ToList())
                {
                    deleteIds.Add(overflow.Id);
                }
            }

            if (deleteIds.Count > 0)
            {
                _dbContext.PageVersions.RemoveRange(pageVersions.Where(version => deleteIds.Contains(version.Id)));
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

        private static string ComputeHash(string content)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
            byte[] hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        private static byte[] Compress(string content)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
            using MemoryStream output = new();
            using (GZipStream gzip = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                gzip.Write(bytes, 0, bytes.Length);
            }

            return output.ToArray();
        }

        private static int CountWords(string plain)
        {
            if (string.IsNullOrWhiteSpace(plain))
            {
                return 0;
            }

            return plain
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Length;
        }
    }
}
