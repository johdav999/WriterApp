using System.Net;
using System.Text.Json;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Quality;

namespace WriterApp.Device.Shared.Services;

public enum GlossaryAvailability { Unavailable, Verified, CachedOffline, CachedRefreshFailed }
public sealed record LocalGlossaryContext(Guid LocalDocumentId, Guid? ServerDocumentId, IReadOnlyList<string> Terms,
    GlossaryAvailability Availability, DateTimeOffset? CheckedAt, string Message, long AccountGeneration);

public sealed class DeviceGlossaryApi(HttpClient http)
{
    public async Task<DeviceGlossarySnapshot> ReadAsync(Guid id, CancellationToken ct, long? expectedAccountGeneration = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/documents/{id}/glossary/device");
        if (expectedAccountGeneration is { } generation) request.Options.Set(DeviceAuthenticatedHandler.ExpectedAccountGeneration, generation);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Glossary refresh unavailable.", null, response.StatusCode);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var bytes = new MemoryStream(); var buffer = new byte[8192]; int count;
        while ((count = await stream.ReadAsync(buffer, ct)) > 0)
        {
            if (bytes.Length + count > DeviceGlossarySnapshot.MaximumBytes) throw new InvalidDataException("Glossary response exceeds the supported size.");
            bytes.Write(buffer, 0, count);
        }
        var result = JsonSerializer.Deserialize<DeviceGlossarySnapshot>(bytes.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow })
            ?? throw new InvalidDataException("Empty glossary response.");
        result.Validate(id);
        return result;
    }
}

/// <summary>Refresh only for an explicit quality check; failures leave ordinary offline checks usable.</summary>
public sealed class DeviceGlossaryService(DeviceGlossaryApi api, LocalGlossaryStore store, LocalDocumentRepository documents,
    DeviceAccountService account, DeviceConnectivity connectivity, DeviceHostOptions host)
{
    public async Task<LocalGlossaryContext> PrepareAsync(LocalDocument source, CancellationToken ct = default)
    {
        long generation = account.Generation;
        LocalGlossaryContext Context(LocalGlossaryCache? cache, GlossaryAvailability state, string message) =>
            new(source.DocumentId, source.ServerDocumentId, cache?.Snapshot.Terms ?? [], state, cache?.CheckedAt, message, generation);
        async Task Check()
        {
            ct.ThrowIfCancellationRequested();
            if (generation != account.Generation) throw new OperationCanceledException("The account changed.", ct);
            var current = await documents.LoadAsync(source.DocumentId, ct);
            if (current is null || current.DeletedAtUtc is not null || current.ServerDocumentId != source.ServerDocumentId)
                throw new InvalidOperationException("The glossary document mapping changed. Run checks again.");
        }
        await Check();
        if (!account.IsSignedIn || string.IsNullOrWhiteSpace(account.AccountId))
            return Context(null, GlossaryAvailability.Unavailable, "Glossary unavailable: sign in to load saved terms. Other local checks are available.");
        if (source.ServerDocumentId is null)
            return Context(null, GlossaryAvailability.Unavailable, "Glossary unavailable: synchronize this document once to load saved terms. Other local checks are available.");
        string scope = LocalBibleStore.ScopeKey(host.ApiBaseAddress, account.AccountId);
        LocalGlossaryCache? cached;
        try { cached = await store.ReadAsync(scope, source, ct); }
        catch (Exception e) when (e is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            // Preserve unknown/corrupt versions for recovery; do not silently overwrite them.
            await Check();
            return Context(null, GlossaryAvailability.Unavailable, "Glossary cache unavailable or unsupported; its file is preserved. Other local checks are available.");
        }
        await Check();
        if (!connectivity.IsOnline)
            return Context(cached, cached is null ? GlossaryAvailability.Unavailable : GlossaryAvailability.CachedOffline,
                cached is null ? "Glossary unavailable offline: no saved terms have been verified for this account and document."
                    : "Using cached glossary offline. Saved terms may have changed since the last successful check.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var snapshot = await api.ReadAsync(source.ServerDocumentId.Value, timeout.Token, generation);
            await Check();
            var updated = new LocalGlossaryCache(1, scope, source.DocumentId, snapshot, DateTimeOffset.UtcNow);
            await store.SaveAsync(updated, timeout.Token);
            await Check();
            return Context(updated, GlossaryAvailability.Verified, snapshot.Terms.Count == 0
                ? "Saved glossary verified empty at this check." : "Saved glossary loaded for this check.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && generation == account.Generation)
        { await Check(); return Failed(cached); }
        catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            await Check();
            return Context(null, GlossaryAvailability.Unavailable, "Glossary access unavailable. Sign in or synchronize this document and check again; cached terms were excluded.");
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidDataException or JsonException or UnauthorizedAccessException
            or DeviceSignInRequiredException or DeviceIdentityUnavailableException)
        { await Check(); return Failed(cached); }
        LocalGlossaryContext Failed(LocalGlossaryCache? previous) => Context(previous,
            previous is null ? GlossaryAvailability.Unavailable : GlossaryAvailability.CachedRefreshFailed,
            previous is null ? "Glossary refresh failed; saved terms are unavailable. Other local checks completed."
                : "Glossary refresh failed. Using cached terms from the last successful check; they may be out of date.");
    }
}
