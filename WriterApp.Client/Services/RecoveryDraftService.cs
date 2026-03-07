using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace WriterApp.Client.Services
{
    public sealed class RecoveryDraftService
    {
        private const string StoragePrefix = "writerapp.recoverydraft.";
        private const string IndexKey = "writerapp.recoverydraft.index";
        private const int SchemaVersion = 1;
        private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(7);
        private readonly IJSRuntime _jsRuntime;

        public RecoveryDraftService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task SaveAsync(RecoveryDraftKey key, string content)
        {
            if (key is null || !key.IsValid)
            {
                return;
            }

            await CleanupExpiredAsync();

            RecoveryDraft draft = new(
                SchemaVersion,
                key,
                content ?? string.Empty,
                ComputeHash(content),
                DateTimeOffset.UtcNow);

            string json = JsonSerializer.Serialize(draft);
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", ToStorageKey(key), json);
                await SaveIndexEntryAsync(key, draft.SavedAtUtc);
            }
            catch (JSException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        public async Task<RecoveryDraft?> LoadAsync(RecoveryDraftKey key)
        {
            if (key is null || !key.IsValid)
            {
                return null;
            }

            await CleanupExpiredAsync();

            string? json = null;
            try
            {
                json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", ToStorageKey(key));
            }
            catch (JSException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                RecoveryDraft? draft = JsonSerializer.Deserialize<RecoveryDraft>(json);
                if (draft is null || draft.SchemaVersion != SchemaVersion)
                {
                    return null;
                }

                if (draft.Key is null || !draft.Key.IsValid)
                {
                    return null;
                }

                if (draft.SavedAtUtc < DateTimeOffset.UtcNow - RetentionWindow)
                {
                    await ClearAsync(key);
                    return null;
                }

                return draft;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public async Task<RecoveryDraft?> GetRestoreCandidateAsync(
            RecoveryDraftKey key,
            string persistedContent,
            DateTimeOffset persistedUpdatedAt)
        {
            RecoveryDraft? draft = await LoadAsync(key);
            if (draft is null)
            {
                return null;
            }

            string persistedHash = ComputeHash(persistedContent);
            if (draft.SavedAtUtc <= persistedUpdatedAt)
            {
                return null;
            }

            if (string.Equals(draft.ContentHash, persistedHash, StringComparison.Ordinal))
            {
                return null;
            }

            return draft;
        }

        public async Task ClearAsync(RecoveryDraftKey key)
        {
            if (key is null || !key.IsValid)
            {
                return;
            }

            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", ToStorageKey(key));
                await RemoveIndexEntryAsync(key);
            }
            catch (JSException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        public async Task CleanupExpiredAsync()
        {
            IReadOnlyList<RecoveryDraftIndexEntry> entries = await LoadIndexAsync();
            if (entries.Count == 0)
            {
                return;
            }

            DateTimeOffset cutoff = DateTimeOffset.UtcNow - RetentionWindow;
            List<RecoveryDraftIndexEntry> active = new();
            bool changed = false;

            foreach (RecoveryDraftIndexEntry entry in entries)
            {
                if (entry.SavedAtUtc < cutoff)
                {
                    changed = true;
                    try
                    {
                        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", ToStorageKey(entry.Key));
                    }
                    catch (JSException)
                    {
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    continue;
                }

                active.Add(entry);
            }

            if (changed)
            {
                await SaveIndexAsync(active);
            }
        }

        private async Task<IReadOnlyList<RecoveryDraftIndexEntry>> LoadIndexAsync()
        {
            string? json = null;
            try
            {
                json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", IndexKey);
            }
            catch (JSException)
            {
                return Array.Empty<RecoveryDraftIndexEntry>();
            }
            catch (InvalidOperationException)
            {
                return Array.Empty<RecoveryDraftIndexEntry>();
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<RecoveryDraftIndexEntry>();
            }

            try
            {
                List<RecoveryDraftIndexEntry>? entries = JsonSerializer.Deserialize<List<RecoveryDraftIndexEntry>>(json);
                if (entries is null)
                {
                    return Array.Empty<RecoveryDraftIndexEntry>();
                }

                return entries;
            }
            catch (JsonException)
            {
                return Array.Empty<RecoveryDraftIndexEntry>();
            }
        }

        private async Task SaveIndexEntryAsync(RecoveryDraftKey key, DateTimeOffset savedAtUtc)
        {
            List<RecoveryDraftIndexEntry> entries = (await LoadIndexAsync()).ToList();
            int existingIndex = entries.FindIndex(entry => Equals(entry.Key, key));
            RecoveryDraftIndexEntry next = new(key, savedAtUtc);
            if (existingIndex >= 0)
            {
                entries[existingIndex] = next;
            }
            else
            {
                entries.Add(next);
            }

            await SaveIndexAsync(entries);
        }

        private async Task RemoveIndexEntryAsync(RecoveryDraftKey key)
        {
            List<RecoveryDraftIndexEntry> entries = (await LoadIndexAsync()).ToList();
            int removed = entries.RemoveAll(entry => Equals(entry.Key, key));
            if (removed == 0)
            {
                return;
            }

            await SaveIndexAsync(entries);
        }

        private async Task SaveIndexAsync(IReadOnlyList<RecoveryDraftIndexEntry> entries)
        {
            string json = JsonSerializer.Serialize(entries);
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", IndexKey, json);
            }
            catch (JSException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static string ToStorageKey(RecoveryDraftKey key)
        {
            return $"{StoragePrefix}{key.Scope}.{key.PrimaryId:D}.{key.SecondaryId?.ToString("D") ?? "none"}.{key.TertiaryId?.ToString("D") ?? "none"}";
        }

        private static string ComputeHash(string? content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return "0";
            }

            byte[] bytes = Encoding.UTF8.GetBytes(content);
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash.AsSpan(0, 8));
        }

        private sealed record RecoveryDraftIndexEntry(RecoveryDraftKey Key, DateTimeOffset SavedAtUtc);
    }

    public sealed record RecoveryDraftKey(
        string Scope,
        Guid PrimaryId,
        Guid? SecondaryId = null,
        Guid? TertiaryId = null)
    {
        public bool IsValid => !string.IsNullOrWhiteSpace(Scope) && PrimaryId != Guid.Empty;

        public static RecoveryDraftKey ForPage(Guid pageId)
        {
            return new("page", pageId);
        }

        public static RecoveryDraftKey ForScene(Guid projectId, Guid sceneNodeId)
        {
            return new("scene", sceneNodeId, projectId);
        }

        public static RecoveryDraftKey ForDocument(Guid documentId)
        {
            return new("document", documentId);
        }
    }

    public sealed record RecoveryDraft(
        int SchemaVersion,
        RecoveryDraftKey Key,
        string Content,
        string ContentHash,
        DateTimeOffset SavedAtUtc);
}
