using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.State
{
    public sealed class DocumentStorageService
    {
        private const string StorageKey = "writerapp.document";
        private const string StorageIndexKey = "writerapp.document.index";
        private const string StoragePrefix = "writerapp.document.";
        private const string AutosavePrefix = "writerapp.document.autosave.";
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<DocumentStorageService> _logger;

        public DocumentStorageService(IJSRuntime jsRuntime, ILogger<DocumentStorageService> logger)
        {
            _jsRuntime = jsRuntime;
            _logger = logger;
        }

        public async Task<List<DocumentIndexEntry>> LoadIndexAsync()
        {
            string? json = null;
            try
            {
                json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageIndexKey);
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage index read failed.");
                return new List<DocumentIndexEntry>();
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<DocumentIndexEntry>();
            }

            List<DocumentIndexEntry>? entries = JsonSerializer.Deserialize<List<DocumentIndexEntry>>(json);
            if (entries is null)
            {
                return new List<DocumentIndexEntry>();
            }

            entries.Sort((a, b) => b.LastModifiedUtc.CompareTo(a.LastModifiedUtc));
            return entries;
        }

        public async Task<Document?> LoadDocumentByIdAsync(string documentId)
        {
            string key = StoragePrefix + documentId;
            string? json = null;
            try
            {
                json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage read failed.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            DocumentStorage? payload = JsonSerializer.Deserialize<DocumentStorage>(json);
            if (payload?.Document is null)
            {
                return null;
            }

            return DocumentFactory.EnsureSynopsis(payload.Document);
        }

        public async Task SaveDocumentAsync(Document document)
        {
            Document normalized = DocumentFactory.EnsureSynopsis(document);
            DocumentStorage payload = new(normalized);
            string json = JsonSerializer.Serialize(payload);

            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StoragePrefix + normalized.DocumentId, json);
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage save failed.");
            }

            await UpdateDocumentIndexAsync(normalized);
        }

        public async Task<bool> SaveAutosaveAsync(Guid documentId, Document document, DateTime autosavedUtc)
        {
            Document normalized = DocumentFactory.EnsureSynopsis(document);
            DocumentAutosave payload = new(normalized, autosavedUtc);
            string json = JsonSerializer.Serialize(payload);

            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", AutosavePrefix + documentId, json);
                return true;
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage autosave failed.");
                return false;
            }
        }

        public async Task<DocumentAutosave?> LoadAutosaveAsync(string documentId)
        {
            string? json = null;
            try
            {
                json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", AutosavePrefix + documentId);
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage autosave read failed.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            DocumentAutosave? autosave = JsonSerializer.Deserialize<DocumentAutosave>(json);
            if (autosave?.Document is null)
            {
                return autosave;
            }

            Document normalized = DocumentFactory.EnsureSynopsis(autosave.Document);
            return new DocumentAutosave(normalized, autosave.AutosavedUtc);
        }

        public async Task<bool> ClearAutosaveAsync(Guid documentId)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AutosavePrefix + documentId);
                return true;
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage autosave clear failed.");
                return false;
            }
        }

        private async Task UpdateDocumentIndexAsync(Document document)
        {
            List<DocumentIndexEntry> entries = await LoadIndexAsync();

            string title = string.IsNullOrWhiteSpace(document.Metadata.Title)
                ? "Untitled"
                : document.Metadata.Title;

            int wordCount = CountWordsInDocument(document);
            DocumentIndexEntry entry = new(document.DocumentId, title, document.Metadata.ModifiedUtc, wordCount);

            int index = entries.FindIndex(item => item.DocumentId == entry.DocumentId);
            if (index >= 0)
            {
                entries[index] = entry;
            }
            else
            {
                entries.Add(entry);
            }

            entries.Sort((a, b) => b.LastModifiedUtc.CompareTo(a.LastModifiedUtc));
            string json = JsonSerializer.Serialize(entries);

            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageIndexKey, json);
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Local storage index update failed.");
            }
        }

        private static int CountWordsInDocument(Document document)
        {
            int wordCount = 0;
            for (int chapterIndex = 0; chapterIndex < document.Chapters.Count; chapterIndex++)
            {
                Chapter chapter = document.Chapters[chapterIndex];
                for (int sectionIndex = 0; sectionIndex < chapter.Sections.Count; sectionIndex++)
                {
                    Section section = chapter.Sections[sectionIndex];
                    string decoded = PlainTextMapper.ToPlainText(section.Content.Value ?? string.Empty);
                    MatchCollection matches = Regex.Matches(decoded, @"\b[\p{L}\p{N}']+\b");
                    wordCount += matches.Count;
                }
            }

            return wordCount;
        }

        public sealed record DocumentAutosave(Document Document, DateTime AutosavedUtc);

        private sealed record DocumentStorage(Document Document);
    }
}
