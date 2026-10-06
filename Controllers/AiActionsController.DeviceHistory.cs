using System.Data;
using System.Security;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Application.Subscriptions;
using WriterApp.Data.AI;
using WriterApp.Shared;

namespace WriterApp.Controllers;

public sealed partial class AiActionsController
{
    private static readonly JsonSerializerOptions HistoryJson = new(JsonSerializerDefaults.Web) { MaxDepth = 32 };

    [HttpGet("history/device")]
    public async Task<ActionResult<DeviceCloudHistory>> DeviceHistory([FromQuery] Guid documentId, CancellationToken ct)
    {
        string owner; try { owner = _userIdResolver.ResolveUserId(User); } catch (SecurityException) { return Unauthorized(); }
        if (documentId == Guid.Empty) return BadRequest();
        if (await _documents.GetAsync(documentId, owner, ct) is null) return NotFound();
        var gate = await EnsureFeatureAllowedAsync(owner, FeatureKey.AiActionHistory, "ai.history"); if (gate is not null) return gate;
        // Bound database materialization and output. Overflow is disclosed; nothing is deleted.
        var records = new List<AiActionHistoryEntryRecord>();
        int bytes = 0;
        await foreach (var record in _dbContext.AiActionHistoryEntries.AsNoTracking().Where(e => e.OwnerUserId == owner && e.DocumentId == documentId)
            .OrderBy(e => e.Id).Take(DeviceAiHistoryContracts.MaxEntries + 1)
            .Select(e => new AiActionHistoryEntryRecord { Id=e.Id, DocumentId=e.DocumentId, SectionId=e.SectionId, PageId=e.PageId,
                ActionKey=e.ActionKey, CreatedAt=e.CreatedAt, ResultJson=e.ResultJson }).AsAsyncEnumerable().WithCancellation(ct))
        {
            bytes += System.Text.Encoding.UTF8.GetByteCount(record.ResultJson);
            if (record.ResultJson.Length > 2_000_000 || bytes > 12 * 1024 * 1024)
                return BadRequest(new { code="history.limit", message="Cloud history exceeds the device inspection size limit. Use web history; the original is retained." });
            records.Add(record);
        }
        bool truncated = records.Count > DeviceAiHistoryContracts.MaxEntries;
        records = records.Take(DeviceAiHistoryContracts.MaxEntries).ToList();
        var ids = records.Select(e => e.Id).ToArray();
        var reports = await _dbContext.DeviceAiHistoryEvents.AsNoTracking().Where(e => e.OwnerUserId == owner && e.DocumentId == documentId && ids.Contains(e.ProposalId))
            .Take(2001).ToListAsync(ct);
        var legacy = await _dbContext.AiActionAppliedEvents.AsNoTracking().Where(e => e.OwnerUserId == owner && ids.Contains(e.HistoryEntryId))
            .Select(e=>new AiActionAppliedEventRecord { HistoryEntryId=e.HistoryEntryId, AppliedAt=e.AppliedAt, UndoneAt=e.UndoneAt }).Take(2001).ToListAsync(ct);
        var web = await _dbContext.WebAiHistoryOperations.AsNoTracking().Where(e=>e.OwnerUserId==owner && e.DocumentId==documentId && e.ReportedAt!=null && ids.Contains(e.ProposalId))
            .Select(e=>new WebAiHistoryOperationRecord {ProposalId=e.ProposalId,ApplicationId=e.ApplicationId,Sequence=e.Sequence,Outcome=e.Outcome}).Take(2001).ToListAsync(ct);
        if (reports.Count > 2000 || legacy.Count > 2000 || web.Count>2000) return BadRequest(new { code = "history.limit", message = "This history has more reporting events than the device inspection limit. Use web history; local recovery remains available." });
        var entries = new List<DeviceCloudHistoryEntry>();
        try
        {
            foreach (var record in records)
            {
                if (record.ResultJson.Length > 2_000_000) throw new InvalidDataException();
                var result = JsonSerializer.Deserialize<AiActionExecuteResponseDto>(record.ResultJson, HistoryJson) ?? throw new InvalidDataException();
                if (result.ProposalId != record.Id || result.ActionKey != record.ActionKey) throw new InvalidDataException();
                var events = reports.Where(e => e.ProposalId == record.Id).ToArray();
                var last = events.OrderByDescending(e => e.ReceivedAt).ThenByDescending(e => e.Sequence).FirstOrDefault();
                var old = legacy.Where(e => e.HistoryEntryId == record.Id).ToArray();
                var outcomes = events.GroupBy(e => e.LocalEntryId).Select(g => g.OrderByDescending(e => e.Sequence).First()).ToArray();
                var webOutcomes=web.Where(e=>e.ProposalId==record.Id).GroupBy(e=>e.ApplicationId).Select(g=>g.MaxBy(e=>e.Sequence)!).ToArray();
                bool applied = outcomes.Any(e => e.State == "Applied") || old.Any(e => e.UndoneAt is null) || webOutcomes.Any(e=>e.Outcome!="Undone");
                string state = applied ? "Applied" : outcomes.Any(e => e.State == "Undone") || old.Length > 0 || webOutcomes.Length>0 ? "Undone"
                    : outcomes.Length > 0 ? "Reviewed" : "Generated";
                var report = last is not null ? JsonSerializer.Deserialize<DeviceAiHistoryReport>(last.ReportJson, HistoryJson) : null;
                entries.Add(new(record.Id, documentId, record.SectionId == Guid.Empty ? null : record.SectionId, record.PageId,
                    record.ActionKey, result.SourceDocumentVersion, record.CreatedAt, result.OriginalText ?? "", result.ProposedText ?? "",
                    state, old.Length + events.Count(e => e.State == "Applied") + web.Count(e=>e.ProposalId==record.Id && e.Sequence==1), report?.Target));
            }
            var snapshot = new DeviceCloudHistory(1, documentId, DateTimeOffset.UtcNow, entries.OrderByDescending(e => e.CreatedAt).ToArray(), truncated);
            DeviceAiHistoryContracts.Validate(snapshot, documentId);
            if (JsonSerializer.SerializeToUtf8Bytes(snapshot, HistoryJson).Length > 16 * 1024 * 1024) throw new InvalidDataException();
            return Ok(snapshot);
        }
        catch (Exception e) when (e is JsonException or InvalidDataException)
        { return BadRequest(new { code = "history.invalid", message = "Stored cloud history exceeds the device contract. Its original is retained; use web history for inspection." }); }
    }

    [HttpPost("history/device/events")]
    [RequestSizeLimit(16384)]
    public async Task<ActionResult<DeviceAiHistoryReceipt>> ReportDeviceHistory([FromBody] DeviceAiHistoryReport report, CancellationToken ct)
    {
        string owner; try { owner = _userIdResolver.ResolveUserId(User); } catch (SecurityException) { return Unauthorized(); }
        try { DeviceAiHistoryContracts.Validate(report); }
        catch (Exception e) when (e is InvalidDataException or NullReferenceException) { return BadRequest(new { code = "history.invalid", message = "Invalid history reporting contract." }); }
        if (await _documents.GetAsync(report.Origin.DocumentId, owner, ct) is null) return NotFound();
        var gate = await EnsureFeatureAllowedAsync(owner, FeatureKey.AiActionHistory, "ai.history"); if (gate is not null) return gate;
        string hash = DeviceAiHistoryContracts.Hash(report);
        return await _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            foreach (var tracked in _dbContext.ChangeTracker.Entries<DeviceAiHistoryEventRecord>().ToArray()) tracked.State = EntityState.Detached;
            await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var receipt = await _dbContext.DeviceAiHistoryEvents.AsNoTracking().SingleOrDefaultAsync(e => e.OwnerUserId == owner && e.OperationId == report.Event.OperationId, ct);
            if (receipt is not null)
            {
                if (receipt.RequestHash != hash) return (ActionResult<DeviceAiHistoryReceipt>)Conflict(new { code = "history.operation_reused", message = "This event ID already identifies another outcome." });
                await tx.CommitAsync(ct); return Ok(HistoryReceipt(receipt));
            }
            var proposal = await _dbContext.AiActionHistoryEntries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == report.Origin.ProposalId && e.OwnerUserId == owner, ct);
            if (proposal is null || proposal.DocumentId != report.Origin.DocumentId || proposal.ActionKey != report.Origin.ActionKey
                || (proposal.SectionId == Guid.Empty ? null : proposal.SectionId) != report.Origin.SectionId || proposal.PageId != report.Origin.PageId) return (ActionResult<DeviceAiHistoryReceipt>)NotFound();
            try
            {
                if (proposal.ResultJson.Length > 2_000_000) throw new InvalidDataException();
                var result = JsonSerializer.Deserialize<AiActionExecuteResponseDto>(proposal.ResultJson, HistoryJson);
                if (result?.SourceDocumentVersion != report.Origin.SourceVersion) return (ActionResult<DeviceAiHistoryReceipt>)Conflict(new { code = "history.source", message = "The reporting source does not match this proposal." });
            }
            catch (Exception e) when (e is JsonException or InvalidDataException) { return (ActionResult<DeviceAiHistoryReceipt>)BadRequest(); }
            var previous = await _dbContext.DeviceAiHistoryEvents.AsNoTracking().Where(e => e.OwnerUserId == owner && e.LocalEntryId == report.LocalEntryId).OrderByDescending(e => e.Sequence).FirstOrDefaultAsync(ct);
            if (report.Event.Sequence != (previous?.Sequence ?? 0) + 1 || report.Event.PreviousOperationId != previous?.OperationId)
                return (ActionResult<DeviceAiHistoryReceipt>)Conflict(new { code = "history.sequence", message = "Deliver the earlier saved event first. No writing was changed." });
            if (previous is not null)
            {
                var oldReport = JsonSerializer.Deserialize<DeviceAiHistoryReport>(previous.ReportJson, HistoryJson)!;
                if (DeviceAiHistoryContracts.Hash(oldReport with { Event = report.Event }) != hash
                    || report.Event.BeforeHash != oldReport.Event.BeforeHash || oldReport.Event.AfterHash is not null && report.Event.AfterHash != oldReport.Event.AfterHash
                    || report.Event.State == previous.State || report.Event.State == "Reviewed"
                    || report.Event.State == "Undone" && previous.State != "Applied") return (ActionResult<DeviceAiHistoryReceipt>)Conflict(new { code = "history.transition" });
            }
            else if (report.Event.State == "Undone") return (ActionResult<DeviceAiHistoryReceipt>)Conflict(new { code = "history.transition" });
            var record = new DeviceAiHistoryEventRecord { OwnerUserId = owner, OperationId = report.Event.OperationId, ProposalId = report.Origin.ProposalId,
                DocumentId = report.Origin.DocumentId, LocalEntryId = report.LocalEntryId, Sequence = report.Event.Sequence, State = report.Event.State,
                ReceivedAt = DateTimeOffset.UtcNow, RequestHash = hash, ReportJson = JsonSerializer.Serialize(report, HistoryJson) };
            _dbContext.DeviceAiHistoryEvents.Add(record); await _dbContext.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(HistoryReceipt(record));
        });
    }
    private static DeviceAiHistoryReceipt HistoryReceipt(DeviceAiHistoryEventRecord record) =>
        new(1, record.OperationId, record.ProposalId, record.LocalEntryId, record.Sequence, record.State, record.RequestHash);
}
