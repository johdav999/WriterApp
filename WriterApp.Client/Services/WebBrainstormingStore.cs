using System.Text.Json;
using Microsoft.JSInterop;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Client.Services;

public sealed class WebBrainstormingStore(IJSRuntime js) : IBrainstormingCacheStore, IAsyncDisposable
{
    private Task<IJSObjectReference>? module;
    private Task<IJSObjectReference> Module() => module ??= js.InvokeAsync<IJSObjectReference>("import", "./js/brainstorming-store.js").AsTask();
    public async Task<string?> ReviewIdentityAsync(string backend, CancellationToken ct) => await (await Module()).InvokeAsync<string?>("reviewIdentity", ct, backend);
    public async Task RememberIdentityAsync(string backend, string? account, CancellationToken ct) => await (await Module()).InvokeVoidAsync("rememberIdentity", ct, backend, account);
    public async Task<bool> IsOfflineAsync(CancellationToken ct) => await (await Module()).InvokeAsync<bool>("isOffline", ct);
    public async Task<BrainstormingCache?> ReadAsync(BrainstormingScope scope, CancellationToken ct = default) {
        var json = await (await Module()).InvokeAsync<string?>("read", ct, BrainstormingPersistence.Partition(scope), scope.DocumentId, scope.SessionId);
        return json is null ? null : Decode(json, scope);
    }
    private static BrainstormingCache Decode(string json, BrainstormingScope scope) {
        if (System.Text.Encoding.UTF8.GetByteCount(json) > BrainstormingPersistence.MaxCacheBytes) throw new InvalidDataException("Oversized retained conversation.");
        var value = JsonSerializer.Deserialize<BrainstormingCache>(json, BrainstormingValidation.Json) ?? throw new InvalidDataException("Missing cache.");
        BrainstormingPersistence.Validate(value); BrainstormingValidation.RequireScope(value.Session.Scope, scope); return value;
    }
    public async Task<BrainstormingCache> WriteAsync(BrainstormingCache value, long expectedRevision, Guid operationId, CancellationToken ct = default) {
        var scope = value.Session.Scope; var current = await ReadAsync(scope, ct); BrainstormingCache prepared;
        try { prepared = BrainstormingPersistence.PrepareWrite(current, value, expectedRevision, operationId); }
        catch (BrainstormingConcurrencyException) {
            await (await Module()).InvokeVoidAsync("retainRejected", ct, BrainstormingPersistence.Partition(scope), scope.DocumentId, scope.SessionId,
                BrainstormingPersistence.Json(new BrainstormingRejectedWrite(1, operationId, expectedRevision, value, DateTimeOffset.UtcNow))); throw;
        }
        if (ReferenceEquals(current, prepared)) return prepared;
        var result = await (await Module()).InvokeAsync<JsonElement>("write", ct, BrainstormingPersistence.Partition(scope), scope.DocumentId, scope.SessionId,
            BrainstormingPersistence.Json(prepared), expectedRevision, operationId,
            BrainstormingPersistence.Json(new BrainstormingRejectedWrite(1, operationId, expectedRevision, value, DateTimeOffset.UtcNow)));
        if (result.GetProperty("conflict").GetBoolean()) throw new BrainstormingConcurrencyException(Decode(result.GetProperty("json").GetString()!, scope).Session);
        return Decode(result.GetProperty("json").GetString()!, scope);
    }
    public async Task<IReadOnlyList<BrainstormingSummary>> ListAsync(BrainstormingScope scope, Guid? after = null, int limit = 25, CancellationToken ct = default) {
        if (limit is < 1 or > 50) throw new InvalidDataException("Use a page size of 1–50.");
        var values = await (await Module()).InvokeAsync<string[]>("list", ct, BrainstormingPersistence.Partition(scope), scope.DocumentId, after, limit);
        if (values.Length > limit) throw new InvalidDataException("Oversized conversation listing.");
        return values.Select(json => { var item = JsonSerializer.Deserialize<BrainstormingSummary>(json, BrainstormingValidation.Json)!;
            BrainstormingValidation.RequireScope(item.Scope, scope with { SessionId = item.Scope.SessionId, ProjectId = item.Scope.ProjectId }); return item; }).ToArray();
    }
    public async Task<IReadOnlyList<BrainstormingRejectedWrite>> ReadRejectedAsync(BrainstormingScope scope, CancellationToken ct = default) {
        var json = await (await Module()).InvokeAsync<string[]>("readRejected", ct, BrainstormingPersistence.Partition(scope), scope.DocumentId, scope.SessionId);
        if (json.Length > 10) throw new InvalidDataException("Oversized conflict listing.");
        return json.Select(raw => { if (System.Text.Encoding.UTF8.GetByteCount(raw) > BrainstormingPersistence.MaxCacheBytes + 1000) throw new InvalidDataException("Oversized conflict.");
            var item = JsonSerializer.Deserialize<BrainstormingRejectedWrite>(raw, BrainstormingValidation.Json)!;
            if (item.Version != 1 || item.OperationId == Guid.Empty) throw new InvalidDataException("Invalid conflict.");
            BrainstormingPersistence.Validate(item.Proposed); BrainstormingValidation.RequireScope(item.Proposed.Session.Scope, scope); return item; }).ToArray();
    }
    public async ValueTask DisposeAsync() { if (module is not null) try { await (await module).DisposeAsync(); } catch (JSDisconnectedException) { } }
}
