using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace WriterApp.Application.State
{
    public sealed class LayoutStateService
    {
        private const string StorageKey = "writerapp.layoutstate.v1";
        private const int SchemaVersion = 1;
        private readonly IJSRuntime _jsRuntime;
        private LayoutState _state = LayoutState.Default;
        private bool _initialized;

        public LayoutStateService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public LayoutState State => _state;

        public event Action<LayoutState>? Changed;

        public async Task InitializeAsync()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            LayoutState? stored = await TryLoadAsync();
            if (stored is not null)
            {
                _state = stored.Normalize();
            }
        }

        public async Task SetStateAsync(LayoutState state)
        {
            LayoutState next = (state ?? LayoutState.Default).Normalize();
            if (next == _state)
            {
                return;
            }

            _state = next;
            await TryPersistAsync(next);
            Changed?.Invoke(_state);
        }

        private async Task<LayoutState?> TryLoadAsync()
        {
            string? json = null;
            try
            {
                json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (JSException)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            LayoutStateStorage? payload = null;
            try
            {
                payload = JsonSerializer.Deserialize<LayoutStateStorage>(json);
            }
            catch (JsonException)
            {
                return null;
            }

            if (payload is null || payload.SchemaVersion != SchemaVersion)
            {
                return null;
            }

            return payload.State ?? LayoutState.Default;
        }

        private async Task TryPersistAsync(LayoutState state)
        {
            LayoutStateStorage payload = new(SchemaVersion, state);
            string json = JsonSerializer.Serialize(payload);

            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
            }
            catch (JSException)
            {
            }
        }

        private sealed record LayoutStateStorage(int SchemaVersion, LayoutState State);
    }
}
