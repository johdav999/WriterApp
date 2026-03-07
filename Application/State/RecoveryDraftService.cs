using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.State
{
    public sealed class RecoveryDraftService
    {
        private const string RecoveryDraftPrefix = "writerapp.document.autosave.";
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<RecoveryDraftService> _logger;

        public RecoveryDraftService(IJSRuntime jsRuntime, ILogger<RecoveryDraftService> logger)
        {
            _jsRuntime = jsRuntime;
            _logger = logger;
        }

        public async Task<bool> SaveDraftAsync(Guid documentId, Document document, DateTime savedAtUtc)
        {
            Document normalized = DocumentFactory.EnsureSynopsis(document);
            RecoveryDraft payload = new(normalized, savedAtUtc);
            string json = JsonSerializer.Serialize(payload);

            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", RecoveryDraftPrefix + documentId, json);
                return true;
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Recovery draft save failed.");
                return false;
            }
        }

        public async Task<RecoveryDraft?> LoadDraftAsync(string documentId)
        {
            string? json = null;
            try
            {
                json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", RecoveryDraftPrefix + documentId);
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Recovery draft read failed.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            RecoveryDraft? draft = JsonSerializer.Deserialize<RecoveryDraft>(json);
            if (draft?.Document is null)
            {
                return draft;
            }

            Document normalized = DocumentFactory.EnsureSynopsis(draft.Document);
            return new RecoveryDraft(normalized, draft.SavedAtUtc);
        }

        public async Task<RecoveryDraft?> GetRestoreCandidateAsync(string documentId, Document persistedDocument)
        {
            RecoveryDraft? draft = await LoadDraftAsync(documentId);
            if (draft is null || draft.SavedAtUtc <= persistedDocument.Metadata.ModifiedUtc)
            {
                return null;
            }

            return draft;
        }

        public async Task<bool> ClearDraftAsync(Guid documentId)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", RecoveryDraftPrefix + documentId);
                return true;
            }
            catch (JSException ex)
            {
                _logger.LogWarning(ex, "Recovery draft clear failed.");
                return false;
            }
        }

        public sealed record RecoveryDraft(Document Document, DateTime SavedAtUtc);
    }
}
