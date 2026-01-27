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

    public sealed record AiActionUndoRedoResult(Guid HistoryEntryId, string Content);

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
            string? beforeContent,
            string? afterContent,
            CancellationToken ct);
        Task<AiActionUndoRedoResult?> UndoAsync(
            string userId,
            Guid documentId,
            Guid sectionId,
            Guid? pageId,
            CancellationToken ct);
        Task<AiActionUndoRedoResult?> RedoAsync(
            string userId,
            Guid documentId,
            Guid sectionId,
            Guid? pageId,
            CancellationToken ct);
    }

    public sealed class InMemoryAiActionHistoryStore : IAiActionHistoryStore
    {
        private readonly ConcurrentDictionary<string, List<AiActionHistoryEntry>> _entries = new();
        private readonly ConcurrentDictionary<Guid, List<AppliedEvent>> _appliedEvents = new();

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
            string? beforeContent,
            string? afterContent,
            CancellationToken ct)
        {
            if (!_entries.Values.Any(list => list.Any(entry => entry.ProposalId == historyEntryId && entry.UserId == userId)))
            {
                throw new InvalidOperationException("History entry not found.");
            }

            List<AppliedEvent> applied = _appliedEvents.GetOrAdd(historyEntryId, _ => new List<AppliedEvent>());
            lock (applied)
            {
                applied.Add(new AppliedEvent(appliedAt, beforeContent, afterContent, null)
                {
                    HistoryEntryId = historyEntryId,
                    UserId = userId,
                    DocumentId = documentId ?? Guid.Empty,
                    SectionId = sectionId ?? Guid.Empty,
                    PageId = pageId
                });
            }

            return Task.CompletedTask;
        }

        public Task<AiActionUndoRedoResult?> UndoAsync(
            string userId,
            Guid documentId,
            Guid sectionId,
            Guid? pageId,
            CancellationToken ct)
        {
            AppliedEvent? target = FindLatestAppliedEvent(userId, documentId, sectionId, pageId, undone: false);
            if (target is null || string.IsNullOrWhiteSpace(target.BeforeContent))
            {
                return Task.FromResult<AiActionUndoRedoResult?>(null);
            }

            target.UndoneAt = DateTimeOffset.UtcNow;
            return Task.FromResult<AiActionUndoRedoResult?>(new AiActionUndoRedoResult(target.HistoryEntryId, target.BeforeContent));
        }

        public Task<AiActionUndoRedoResult?> RedoAsync(
            string userId,
            Guid documentId,
            Guid sectionId,
            Guid? pageId,
            CancellationToken ct)
        {
            AppliedEvent? target = FindLatestAppliedEvent(userId, documentId, sectionId, pageId, undone: true);
            if (target is null || string.IsNullOrWhiteSpace(target.AfterContent))
            {
                return Task.FromResult<AiActionUndoRedoResult?>(null);
            }

            target.UndoneAt = null;
            target.AppliedAt = DateTimeOffset.UtcNow;
            return Task.FromResult<AiActionUndoRedoResult?>(new AiActionUndoRedoResult(target.HistoryEntryId, target.AfterContent));
        }

        private AiActionHistoryEntry ApplyInMemoryAppliedState(AiActionHistoryEntry entry)
        {
            if (!_appliedEvents.TryGetValue(entry.ProposalId, out List<AppliedEvent>? applied))
            {
                return entry with { IsApplied = false, AppliedCount = 0, LastAppliedAt = null };
            }

            lock (applied)
            {
                if (applied.Count == 0)
                {
                    return entry with { IsApplied = false, AppliedCount = 0, LastAppliedAt = null };
                }

                int appliedCount = applied.Count;
                DateTimeOffset lastApplied = applied.Max(item => item.AppliedAt);
                bool isApplied = applied.Any(item => item.UndoneAt is null);
                return entry with { IsApplied = isApplied, AppliedCount = appliedCount, LastAppliedAt = lastApplied };
            }
        }

        private AppliedEvent? FindLatestAppliedEvent(
            string userId,
            Guid documentId,
            Guid sectionId,
            Guid? pageId,
            bool undone)
        {
            IEnumerable<AppliedEvent> all = _appliedEvents.Values.SelectMany(list => list);
            IEnumerable<AppliedEvent> filtered = all
                .Where(item => item.UserId == userId)
                .Where(item => item.DocumentId == documentId)
                .Where(item => item.SectionId == sectionId)
                .Where(item => item.PageId == pageId);

            if (undone)
            {
                return filtered
                    .Where(item => item.UndoneAt.HasValue)
                    .OrderByDescending(item => item.UndoneAt)
                    .FirstOrDefault();
            }

            return filtered
                .Where(item => item.UndoneAt is null)
                .OrderByDescending(item => item.AppliedAt)
                .FirstOrDefault();
        }

        private sealed class AppliedEvent
        {
            public AppliedEvent(DateTimeOffset appliedAt, string? beforeContent, string? afterContent, DateTimeOffset? undoneAt)
            {
                AppliedAt = appliedAt;
                BeforeContent = beforeContent;
                AfterContent = afterContent;
                UndoneAt = undoneAt;
            }

            public Guid HistoryEntryId { get; init; }
            public string UserId { get; init; } = string.Empty;
            public Guid DocumentId { get; init; }
            public Guid SectionId { get; init; }
            public Guid? PageId { get; init; }
            public DateTimeOffset AppliedAt { get; set; }
            public string? BeforeContent { get; }
            public string? AfterContent { get; }
            public DateTimeOffset? UndoneAt { get; set; }
        }
    }
}
