using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        string? ProposedText);

    public interface IAiActionHistoryStore
    {
        Task AddAsync(AiActionHistoryEntry entry, CancellationToken ct);
        Task<IReadOnlyList<AiActionHistoryEntry>> ListAsync(string userId, Guid documentId, CancellationToken ct);
    }

    public sealed class InMemoryAiActionHistoryStore : IAiActionHistoryStore
    {
        private readonly ConcurrentDictionary<string, List<AiActionHistoryEntry>> _entries = new();

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
                return Task.FromResult<IReadOnlyList<AiActionHistoryEntry>>(list.ToList());
            }
        }
    }
}
