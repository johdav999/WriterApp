using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Continuity;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Shared.Canon;

namespace WriterApp.Controllers;

public sealed partial class DocumentBiblesController
{
    // Separate additive routes avoid attributing stronger guarantees to the legacy web DTO.
    [HttpGet("{bibleType}/device")]
    public Task<ActionResult<DeviceBibleSnapshot>> GetDeviceSnapshot(Guid documentId, string bibleType,
        [FromQuery] string expectedDocumentVersion, CancellationToken ct) => DeviceSnapshotAsync(
            documentId, bibleType, expectedDocumentVersion, null, ct);

    [HttpPost("{bibleType}/device/refresh")]
    public Task<ActionResult<DeviceBibleSnapshot>> RefreshDeviceSnapshot(Guid documentId, string bibleType,
        [FromBody] DeviceBibleRefreshRequest request, CancellationToken ct) => DeviceSnapshotAsync(
            documentId, bibleType, request.ExpectedDocumentVersion, request, ct);

    private async Task<ActionResult<DeviceBibleSnapshot>> DeviceSnapshotAsync(Guid documentId, string bibleType,
        string version, DeviceBibleRefreshRequest? request, CancellationToken ct)
    {
        if (!TryParseBibleType(bibleType, out var type) || !Enum.IsDefined(type)
            || string.IsNullOrWhiteSpace(version) || version.Length > 200
            || (request is not null && (string.IsNullOrWhiteSpace(request.ExpectedSnapshotVersion) || request.ExpectedSnapshotVersion.Length > 200)))
            return BadRequest(new { code = "bible_invalid_request", message = "Choose a supported canon type and source revision." });
        string owner;
        try { owner = _userIdResolver.ResolveUserId(User); }
        catch (System.Security.SecurityException) { return Unauthorized(); }
        var gate = await EnsureFeatureAllowedAsync(owner, request is null ? FeatureKey.StoryCanon : FeatureKey.CanonRefresh, "story.canon");
        if (gate is not null) return gate;
        var record = await _documents.GetAsync(documentId, owner, ct);
        if (record is null) return NotFound();
        if(request?.WebSource is { } webSource) {
            try { if(webSource.DocumentId!=documentId || webSource.SectionId!=request.ActiveSectionId)throw new InvalidDataException("Wrong canon source.");await new WriterApp.Application.AI.WebAiSourceService(_dbContext,owner).Require(webSource,ct); }
            catch(Exception e) when(e is WriterApp.Application.Documents.DocumentSyncException or InvalidDataException or InvalidOperationException) { return Stale(); }
        }
        if (request?.WebSource is null && !await MatchesVersion(documentId, owner, version, ct)) return Stale();
        var sections = await _sections.ListByDocumentAsync(documentId, owner, ct);
        var source = await BuildDocumentAsync(record, sections, owner, ct);
        if (request?.WebSource is null && !await MatchesVersion(documentId, owner, version, ct)) return Stale();
        var existing = await _bibleStore.GetSnapshotAsync(documentId, type, ct);
        if (request is not null && SnapshotToken(existing) != request.ExpectedSnapshotVersion) return Stale();
        PreparedBibleRefresh? prepared = null;
        if (request is not null)
        {
            if (sections.Count == 0 || request.ActiveSectionId is { } active && !sections.Any(s => s.Id == active))
                return BadRequest(new { code = "bible_invalid_section", message = "Choose a scene from this manuscript." });
            try
            {
                prepared = await _refreshService.PrepareAsync(source, owner,
                    request.ActiveSectionId ?? sections.OrderBy(s => s.OrderIndex).First().Id, type, request.FullRebuild, ct);
                if (SnapshotToken(prepared.Existing) != request.ExpectedSnapshotVersion) return Stale();
                _ = CanonContent.Parse((CanonKind)type, prepared.ContentJson);
                if(request.WebSource is not null && prepared.ContentJson.Length>100_000)throw new InvalidDataException("Checked web canon exceeds 100,000 characters.");
            }
            catch (EntitlementDeniedException ex) { return StatusCode(402, new { code = "entitlement_denied", message = ex.Message }); }
            catch (Exception ex) when (ex is BibleRefreshInvalidPayloadException or InvalidDataException or JsonException)
            { return UnprocessableEntity(new { code = "bible_invalid_output", message = "Canon returned invalid structured data. Previous canon is preserved." }); }
            catch (InvalidOperationException) { return StatusCode(502, new { code = "bible_unavailable", message = "Canon generation failed. Previous canon is preserved." }); }
        }
        // No provider call inside this short serializable transaction. The version read
        // protects against concurrent document changes until the snapshot save commits.
        return await _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (request?.WebSource is null && !await MatchesVersion(documentId, owner, version, ct)) return Stale();
            var latest = await _bibleStore.GetSnapshotAsync(documentId, type, ct);
            if (SnapshotToken(latest) != SnapshotToken(existing)) return Stale();
            if(request?.WebSource is { } completed) {
                try { await new WriterApp.Application.AI.WebAiSourceService(_dbContext,owner).Require(completed,ct); }
                catch(Exception e) when(e is WriterApp.Application.Documents.DocumentSyncException or InvalidDataException or InvalidOperationException) { return Stale(); }
            }
            if (prepared is not null) latest = await _refreshService.CommitAsync(prepared, ct);
            var sourceHash = BibleRefreshService.SourceHash(source);
            var content = latest?.ContentJson ?? BibleJson.EmptyBibleContent(type);
            try { content = BiblePatchApplier.NormalizeContent(type, content); }
            catch (Exception ex) when (ex is InvalidDataException or JsonException)
            { return UnprocessableEntity(new { code = "bible_invalid_output", message = "Stored canon is unavailable in this format. Previous cache is preserved." }); }
            var result = new DeviceBibleSnapshot(1, documentId, (CanonKind)type, SnapshotToken(latest),
                latest?.LastRefreshSourceHash == sourceHash ? version : null, version, latest?.LastRefreshSourceHash ?? "",
                latest?.LastRefreshUtc, content, latest is null ? sections.Count : CountChangedSections(latest.Cursor, source), latest is not null);
            await transaction.CommitAsync(ct);
            return (ActionResult<DeviceBibleSnapshot>)Ok(result);
        });
    }
    private Task<bool> MatchesVersion(Guid documentId, string owner, string version, CancellationToken ct) =>
        _dbContext.DocumentSyncRecords.AsNoTracking().AnyAsync(d => d.DocumentId == documentId
            && d.OwnerUserId == owner && d.Version == version && !d.IsDeleted && !d.IsTrashed, ct);
    private ActionResult Stale() => Conflict(new { code = "bible_stale_source", message = "The manuscript or canon changed. Synchronize and load canon before retrying." });
    public static string SnapshotToken(BibleSnapshotState? snapshot) => snapshot is null ? "missing" :
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(snapshot)));
}
