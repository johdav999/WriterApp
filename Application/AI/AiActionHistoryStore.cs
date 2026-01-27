using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WriterApp.Application.AI
{
    public sealed record AiActionHistoryEntry(
        Guid ProposalId,
        string ActionKey,
        string UserId,
        Guid DocumentId,
        Guid SectionId,
        DateTimeOffset CreatedUtc,
        string? Summary,
        string? OriginalText,
        string? ProposedText,
        Guid? PageId = null,
        string? ProviderId = null,
        string? ModelId = null,
        string? RequestJson = null,
        string? ResultJson = null,
        bool IsApplied = false,
        DateTimeOffset? LastAppliedAt = null,
        int AppliedCount = 0);

    public interface IAiActionHistoryStore
    {
        Task AddAsync(AiActionHistoryEntry entry, CancellationToken ct);
        Task<IReadOnlyList<AiActionHistoryEntry>> ListAsync(string userId, Guid documentId, CancellationToken ct);
        Task AddAppliedEventAsync(
            string userId,
            Guid historyEntryId,
            DateTimeOffset appliedAt,
            Guid? documentId,
            Guid? sectionId,
            Guid? pageId,
            CancellationToken ct);
    }

    public sealed class InMemoryAiActionHistoryStore : IAiActionHistoryStore
    {
        private readonly ConcurrentDictionary<string, List<AiActionHistoryEntry>> _entries = new();
        private readonly ConcurrentDictionary<Guid, List<DateTimeOffset>> _appliedEvents = new();

        public Task AddAsync(AiActionHistoryEntry entry, CancellationToken ct)
        {
            // TODO: Replace with a persistent store once AI history is migrated to DB-backed documents.
            string key = $"{entry.UserId}:{entry.DocumentId}";
            List<AiActionHistoryEntry> list = _entries.GetOrAdd(key, _ => new List<AiActionHistoryEntry>());
            lock (list)
            {
                list.Add(entry);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AiActionHistoryEntry>> ListAsync(string userId, Guid documentId, CancellationToken ct)
        {
            string key = $"{userId}:{documentId}";
            if (!_entries.TryGetValue(key, out List<AiActionHistoryEntry>? list))
            {
                return Task.FromResult<IReadOnlyList<AiActionHistoryEntry>>(Array.Empty<AiActionHistoryEntry>());
            }

            lock (list)
            {
                List<AiActionHistoryEntry> result = list
                    .Select(entry => ApplyInMemoryAppliedState(entry))
                    .ToList();
                return Task.FromResult<IReadOnlyList<AiActionHistoryEntry>>(result);
            }
        }

        public Task AddAppliedEventAsync(
            string userId,
            Guid historyEntryId,
            DateTimeOffset appliedAt,
            Guid? documentId,
            Guid? sectionId,
            Guid? pageId,
            CancellationToken ct)
        {
            if (!_entries.Values.Any(list => list.Any(entry => entry.ProposalId == historyEntryId && entry.UserId == userId)))
            {
                throw new InvalidOperationException("History entry not found.");
            }

            List<DateTimeOffset> applied = _appliedEvents.GetOrAdd(historyEntryId, _ => new List<DateTimeOffset>());
            lock (applied)
            {
                applied.Add(appliedAt);
            }

            return Task.CompletedTask;
        }

        private AiActionHistoryEntry ApplyInMemoryAppliedState(AiActionHistoryEntry entry)
        {
            if (!_appliedEvents.TryGetValue(entry.ProposalId, out List<DateTimeOffset>? applied))
            {
                return entry with { IsApplied = false, AppliedCount = 0, LastAppliedAt = null };
            }

            lock (applied)
            {
                if (applied.Count == 0)
                {
                    return entry with { IsApplied = false, AppliedCount = 0, LastAppliedAt = null };
                }

                DateTimeOffset lastApplied = applied.Max();
                return entry with { IsApplied = true, AppliedCount = applied.Count, LastAppliedAt = lastApplied };
            }
        }
    }
}
