using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Data.AI;

namespace WriterApp.Application.AI
{
    public sealed class EfCoreAiActionHistoryStore : IAiActionHistoryStore
    {
        private readonly AppDbContext _dbContext;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public EfCoreAiActionHistoryStore(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task AddAsync(AiActionHistoryEntry entry, CancellationToken ct)
        {
            if (entry is null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            AiActionHistoryEntryRecord record = new()
            {
                Id = entry.ProposalId,
                OwnerUserId = entry.UserId,
                DocumentId = entry.DocumentId,
                SectionId = entry.SectionId,
                PageId = entry.PageId,
                ActionKey = entry.ActionKey,
                ProviderId = entry.ProviderId,
                ModelId = entry.ModelId,
                RequestJson = entry.RequestJson ?? "{}",
                ResultJson = entry.ResultJson ?? "{}",
                CreatedAt = entry.CreatedUtc
            };

            _dbContext.AiActionHistoryEntries.Add(record);
            await _dbContext.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<AiActionHistoryEntry>> ListAsync(string userId, Guid documentId, CancellationToken ct)
        {
            List<AiActionHistoryEntryRecord> records = await _dbContext.AiActionHistoryEntries
                .AsNoTracking()
                .Where(entry => entry.OwnerUserId == userId && entry.DocumentId == documentId)
                .OrderByDescending(entry => entry.CreatedAt)
                .ToListAsync(ct);

            if (records.Count == 0)
            {
                return Array.Empty<AiActionHistoryEntry>();
            }

            Guid[] ids = records.Select(record => record.Id).ToArray();
            Dictionary<Guid, AppliedStats> applied = await _dbContext.AiActionAppliedEvents
                .AsNoTracking()
                .Where(evt => evt.OwnerUserId == userId && ids.Contains(evt.HistoryEntryId))
                .GroupBy(evt => evt.HistoryEntryId)
                .Select(group => new AppliedStats(
                    group.Key,
                    group.Count(),
                    group.Max(evt => evt.AppliedAt)))
                .ToDictionaryAsync(stat => stat.HistoryEntryId, ct);

            return records.Select(record => MapToEntry(record, applied)).ToList();
        }

        public async Task AddAppliedEventAsync(
            string userId,
            Guid historyEntryId,
            DateTimeOffset appliedAt,
            Guid? documentId,
            Guid? sectionId,
            Guid? pageId,
            CancellationToken ct)
        {
            bool exists = await _dbContext.AiActionHistoryEntries
                .AsNoTracking()
                .AnyAsync(entry => entry.Id == historyEntryId && entry.OwnerUserId == userId, ct);
            if (!exists)
            {
                throw new InvalidOperationException("History entry not found.");
            }

            AiActionAppliedEventRecord record = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                HistoryEntryId = historyEntryId,
                AppliedAt = appliedAt,
                AppliedToDocumentId = documentId,
                AppliedToSectionId = sectionId,
                AppliedToPageId = pageId
            };

            _dbContext.AiActionAppliedEvents.Add(record);
            await _dbContext.SaveChangesAsync(ct);
        }

        private static AiActionHistoryEntry MapToEntry(AiActionHistoryEntryRecord record, Dictionary<Guid, AppliedStats> applied)
        {
            AiActionExecuteResponseDto? response = TryReadResponse(record.ResultJson);
            bool isApplied = applied.TryGetValue(record.Id, out AppliedStats? stats) && stats.AppliedCount > 0;
            int appliedCount = stats?.AppliedCount ?? 0;
            DateTimeOffset? lastAppliedAt = stats?.LastAppliedAt;

            return new AiActionHistoryEntry(
                record.Id,
                record.ActionKey,
                record.OwnerUserId,
                record.DocumentId ?? Guid.Empty,
                record.SectionId ?? Guid.Empty,
                record.CreatedAt,
                response?.ChangesSummary,
                response?.OriginalText,
                response?.ProposedText,
                record.PageId,
                record.ProviderId,
                record.ModelId,
                record.RequestJson,
                record.ResultJson,
                isApplied,
                lastAppliedAt,
                appliedCount);
        }

        private static AiActionExecuteResponseDto? TryReadResponse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<AiActionExecuteResponseDto>(json, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private sealed record AppliedStats(Guid HistoryEntryId, int AppliedCount, DateTimeOffset? LastAppliedAt);
    }
}
