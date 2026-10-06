using System.Data;
using System.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Application.State;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Shared.Quality;

namespace WriterApp.Controllers;

[ApiController, Authorize, Route("api/pages/{pageId:guid}/quality-checks/device-decisions")]
public sealed class DeviceQualityDismissalsController(AppDbContext db, IPageRepository pages, IUserIdResolver users) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Exchange(Guid pageId, QualityDismissalRequest request, CancellationToken ct)
    {
        if (request is null || request.Version != 1 || request.DocumentId == Guid.Empty || !QualityDismissalIdentity.IsHash(request.SourceHash)
            || request.Decisions is null || request.Decisions.Count > 200
            || request.Decisions.Any(d => d is null || !QualityDismissalIdentity.IsHash(d.IssueKey))
            || request.Decisions.Select(d => d.IssueKey).Distinct().Count() != request.Decisions.Count)
            return BadRequest(new { message = "Invalid quality decision contract." });
        try
        {
            string owner = users.ResolveUserId(User);
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                // Use the same mutation lock as document sync and checked recovery.
                await db.Database.ExecuteSqlRawAsync("UPDATE DocumentSyncClocks SET Sequence=Sequence WHERE Id=1", ct);
                var page = await pages.GetAsync(pageId, owner, ct);
                if (page is null || page.DocumentId != request.DocumentId) return (IActionResult)NotFound();
                if (page.Content.Length > 500_000) return BadRequest();
                var text = PlainTextMapper.ToPlainText(page.Content);
                var glossary = await db.DocumentGlossaryEntries.AsNoTracking().Where(g => g.DocumentId == page.DocumentId)
                    .OrderBy(g => g.Term).Select(g => g.Term).ToListAsync(ct);
                if (QualityDismissalIdentity.Source(text, glossary) != request.SourceHash)
                    return Conflict(new { message = "The saved source, glossary or rule revision differs. Decisions remain local; check again after synchronization." });
                var context = new QualityCheckContext(text, QualityTextAnalyzer.GetTokens(text), QualityTextAnalyzer.GetSentences(text),
                    QualityTextAnalyzer.GetParagraphs(text), glossary);
                var issues = new QualityCheckEngine(QualityRuleCatalog.Create()).Evaluate(context, 200);
                var keys = issues.Select(i => i.IssueKey).ToHashSet(StringComparer.Ordinal);
                if (request.Decisions.Any(d => !keys.Contains(d.IssueKey))) return Conflict(new { message = "A finding no longer maps to this saved page." });
                var saved = await db.PageQualityIssueDismissals.Where(d => d.UserId == owner && d.PageId == pageId).ToListAsync(ct);
                foreach (var decision in request.Decisions)
                {
                    string key = QualityDismissalIdentity.Key(request.SourceHash, decision.IssueKey);
                    if (decision.Dismissed)
                    {
                        if (!saved.Any(d => d.IssueKey == key))
                            db.PageQualityIssueDismissals.Add(new PageQualityIssueDismissalRecord { UserId = owner, PageId = pageId,
                                IssueKey = key, DismissedAt = DateTimeOffset.UtcNow });
                    }
                    else
                    {
                        // Only an explicit Restore removes a matching legacy client decision.
                        db.PageQualityIssueDismissals.RemoveRange(saved.Where(d => d.IssueKey == key || d.IssueKey == decision.IssueKey));
                    }
                }
                await db.SaveChangesAsync(ct);
                var stored = await db.PageQualityIssueDismissals.AsNoTracking().Where(d => d.UserId == owner && d.PageId == pageId)
                    .Select(d => d.IssueKey).ToListAsync(ct);
                var receipt = new QualityDismissalReceipt(1, page.DocumentId, pageId, request.SourceHash, issues.Select(i =>
                    new QualityDismissalDecision(i.IssueKey, stored.Contains(QualityDismissalIdentity.Key(request.SourceHash, i.IssueKey)))).ToArray(),
                    issues.Where(i => stored.Contains(i.IssueKey)).Select(i => i.IssueKey).ToArray());
                await tx.CommitAsync(ct);
                return Ok(receipt);
            });
        }
        catch (SecurityException) { return Unauthorized(); }
        catch (DbUpdateException) { return StatusCode(503, new { message = "Quality decisions could not be saved. Retry the same decisions." }); }
    }
}
