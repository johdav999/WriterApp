using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace WriterApp.Client.State
{
    public sealed class LastOpenedDocumentStateService
    {
        private const string StorageKey = "writer:lastOpenedDoc";
        private const int SchemaVersion = 1;
        private readonly IJSRuntime _jsRuntime;
        private LastOpenedDocumentState? _state;
        private bool _loaded;

        public LastOpenedDocumentStateService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public LastOpenedDocumentState? State => _state;

        public bool IsLoaded => _loaded;

        public async Task LoadAsync()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            string? json = null;
            try
            {
                json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);
            }
            catch (InvalidOperationException)
            {
                return;
            }
            catch (JSException)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            LastOpenedDocumentStorage? payload = null;
            try
            {
                payload = JsonSerializer.Deserialize<LastOpenedDocumentStorage>(json);
            }
            catch (JsonException)
            {
                return;
            }

            if (payload is null || payload.SchemaVersion != SchemaVersion)
            {
                return;
            }

            if (payload.State?.DocumentId is null || payload.State.DocumentId == Guid.Empty)
            {
                return;
            }

            _state = payload.State;
        }

        public async Task SaveAsync(Guid documentId, Guid? sectionId)
        {
            if (documentId == Guid.Empty)
            {
                return;
            }

            LastOpenedDocumentState next = new(documentId, sectionId, DateTimeOffset.UtcNow);
            if (_state?.DocumentId == next.DocumentId && _state?.SectionId == next.SectionId)
            {
                _state = next;
                return;
            }

            _state = next;
            _loaded = true;
            await PersistAsync();
        }

        public async Task ClearAsync()
        {
            _state = null;
            _loaded = true;
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            }
            catch (JSException)
            {
            }
        }

        private async Task PersistAsync()
        {
            if (_state is null)
            {
                return;
            }

            LastOpenedDocumentStorage payload = new(SchemaVersion, _state);
            string json = JsonSerializer.Serialize(payload);

            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
            }
            catch (JSException)
            {
            }
        }

        private sealed record LastOpenedDocumentStorage(int SchemaVersion, LastOpenedDocumentState State);
    }

    public sealed record LastOpenedDocumentState(Guid DocumentId, Guid? SectionId, DateTimeOffset UpdatedAt);
}
