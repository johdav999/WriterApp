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

            return records.Select(MapToEntry).ToList();
        }

        private static AiActionHistoryEntry MapToEntry(AiActionHistoryEntryRecord record)
        {
            AiActionExecuteResponseDto? response = TryReadResponse(record.ResultJson);

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
                record.ResultJson);
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
    }
}
