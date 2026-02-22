using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Data.Continuity;

namespace WriterApp.Application.Continuity
{
    public sealed class EfCoreBibleStore : IBibleStore
    {
        private readonly AppDbContext _dbContext;

        public EfCoreBibleStore(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<BibleSnapshotState?> GetSnapshotAsync(Guid documentId, BibleType bibleType, CancellationToken ct)
        {
            string bibleTypeKey = bibleType.ToString();
            BibleSnapshotRecord? record = await _dbContext.BibleSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    entry => entry.DocumentId == documentId && entry.BibleType == bibleTypeKey,
                    ct);
            return record is null ? null : Map(record, bibleType);
        }

        public async Task<BibleSnapshotState> UpsertSnapshotAsync(
            Guid documentId,
            BibleType bibleType,
            string contentJson,
            string sourceHash,
            BibleRefreshCursor cursor,
            BibleRefreshStats stats,
            CancellationToken ct)
        {
            string bibleTypeKey = bibleType.ToString();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            BibleSnapshotRecord? record = await _dbContext.BibleSnapshots
                .FirstOrDefaultAsync(
                    entry => entry.DocumentId == documentId && entry.BibleType == bibleTypeKey,
                    ct);

            if (record is null)
            {
                record = new BibleSnapshotRecord
                {
                    Id = Guid.NewGuid(),
                    DocumentId = documentId,
                    BibleType = bibleTypeKey,
                    CreatedUtc = now
                };
                _dbContext.BibleSnapshots.Add(record);
            }

            record.SchemaVersion = 1;
            record.ContentJson = string.IsNullOrWhiteSpace(contentJson) ? BibleJson.EmptyBibleContent(bibleType) : contentJson;
            record.UpdatedUtc = now;
            record.LastRefreshUtc = now;
            record.LastRefreshSourceHash = sourceHash ?? string.Empty;
            record.LastRefreshCursorJson = System.Text.Json.JsonSerializer.Serialize(cursor, BibleJson.JsonOptions);
            record.LastRefreshStatsJson = System.Text.Json.JsonSerializer.Serialize(stats, BibleJson.JsonOptions);

            await _dbContext.SaveChangesAsync(ct);
            return Map(record, bibleType);
        }

        private static BibleSnapshotState Map(BibleSnapshotRecord record, BibleType bibleType)
        {
            _ = BibleJson.TryParseCursor(record.LastRefreshCursorJson, out BibleRefreshCursor cursor);
            _ = BibleJson.TryParseStats(record.LastRefreshStatsJson, out BibleRefreshStats stats);
            return new BibleSnapshotState(
                record.Id,
                record.DocumentId,
                bibleType,
                record.SchemaVersion,
                record.ContentJson ?? BibleJson.EmptyBibleContent(bibleType),
                record.CreatedUtc,
                record.UpdatedUtc,
                record.LastRefreshUtc,
                record.LastRefreshSourceHash ?? string.Empty,
                stats,
                cursor);
        }
    }
}
