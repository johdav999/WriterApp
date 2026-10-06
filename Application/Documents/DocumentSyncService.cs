using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Search;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Shared.Sync;
using WriterApp.Shared.Editor;

namespace WriterApp.Application.Documents;

public sealed class DocumentSyncException(int status, string code, string message, SyncChange? current = null) : Exception(message)
{
    public int Status { get; } = status;
    public SyncError Error { get; } = new(code, message, current);
}

public sealed partial class DocumentSyncService(AppDbContext db, IUserEntitlementStore entitlements,
    IDataProtectionProvider protection, ProjectDeletionService deletion, ISearchIndexBackfillQueue search)
{
    public const string Entitlement = "documents.sync";
    public const int MaxRequestBytes = 5 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static DocumentSyncException Invalid(string message) => new(400, "invalid_sync_request", message);
    private static SyncChange State(DocumentSyncRecord record) => new(record.DocumentId, record.Version, record.IsDeleted, record.IsTrashed);

    private async Task AuthorizeAsync(string owner, CancellationToken ct, Guid? onboardingDocument = null)
    {
        if (string.IsNullOrWhiteSpace(owner)) throw new DocumentSyncException(401, "authentication_required", "Sign in to synchronize.");
        if(onboardingDocument is { } demo) {
            if(!await db.OnboardingDemoWorkspaces.AnyAsync(x => x.OwnerUserId==owner && x.DocumentId==demo,ct))
                throw new DocumentSyncException(404,"demo_not_found","This document is not the server-owned demo for this account.");
            return;
        }
        // Read current subscription state, rather than the one-minute feature-display cache.
        if (!EntitlementAccessEvaluator.Evaluate(await entitlements.GetOrCreateAsync(owner, ct)).IsPaidAccessActive)
            throw new DocumentSyncException(403, "entitlement_required", "documents.sync requires an active paid plan. Local editing remains available.");
    }

    public async Task<SyncChanges> ChangesAsync(string owner, string? cursor, int limit, CancellationToken ct = default)
    {
        await AuthorizeAsync(owner, ct);
        if (limit is < 1 or > 100) throw Invalid("Page size must be between 1 and 100.");
        var protector = protection.CreateProtector("WriterApp.DocumentSync.Cursor.v1", owner);
        long sequence = 0;
        if (cursor is not null)
        {
            try
            {
                if (cursor.Length > 2048 || !long.TryParse(protector.Unprotect(cursor), NumberStyles.None, CultureInfo.InvariantCulture, out sequence) || sequence < 0)
                    throw Invalid("Invalid cursor; restart change discovery without a cursor.");
            }
            catch (CryptographicException) { throw Invalid("Invalid cursor; restart change discovery without a cursor."); }
        }
        var records = await db.DocumentSyncRecords.AsNoTracking().Where(x => x.OwnerUserId == owner && x.Sequence > sequence)
            .OrderBy(x => x.Sequence).Take(limit + 1).ToListAsync(ct);
        bool more = records.Count > limit;
        var page = records.Take(limit).ToArray();
        return new(page.Select(State).ToArray(), protector.Protect((page.LastOrDefault()?.Sequence ?? sequence).ToString(CultureInfo.InvariantCulture)), more);
    }

    public async Task<SyncSnapshot> DownloadAsync(string owner, Guid id, CancellationToken ct = default, bool projects = false, bool planning = false, bool multipleDocuments = false, bool onboardingDemo = false)
    {
        await AuthorizeAsync(owner, ct, onboardingDemo ? id : null);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var state = await OwnedStateAsync(owner, id, ct);
            var document = state.IsDeleted ? null : await db.Documents.AsNoTracking().Include(x => x.Sections).ThenInclude(x => x.Pages)
                .SingleAsync(x => x.Id == id && x.OwnerUserId == owner, ct);
            var dto = document is null ? null : ToDto(document);
            if (projects && document is not null) dto = dto! with { Project = await ReadProjectAsync(document, ct, planning, multipleDocuments) };
            if (projects && document is not null) state = await OwnedStateAsync(owner, id, ct);
            var result = new SyncSnapshot(State(state), dto);
            await transaction.CommitAsync(ct);
            return result;
        });
    }

    private async Task<DocumentSyncRecord> OwnedStateAsync(string owner, Guid id, CancellationToken ct) =>
        await db.DocumentSyncRecords.AsNoTracking().SingleOrDefaultAsync(x => x.DocumentId == id && x.OwnerUserId == owner, ct)
        ?? throw new DocumentSyncException(404, "document_not_found", "Document not found.");

    public async Task<SyncMutationResult> MutateAsync(string owner, Guid id, SyncMutation request, CancellationToken ct = default, bool projects = false, bool planning = false, bool multipleDocuments = false, bool onboardingDemo = false)
    {
        await AuthorizeAsync(owner, ct, onboardingDemo ? id : null);
        Validate(id, request);
        if(onboardingDemo && request.Action=="upload" && !await db.DocumentSyncRecords.AnyAsync(x => x.OwnerUserId==owner && x.DocumentId==id && !x.IsDeleted,ct))
            throw new DocumentSyncException(409,"demo_deleted","A missing demo cannot be recreated. Restore or recover its existing identity.");
        if (request.Document?.Project is { Version: >= 3 } && !multipleDocuments) throw MultipleDocumentCapability();
        if (request.Document?.Project is not null && !projects) throw ProjectCapability();
        if (request.Document?.Project is { } proposed) { if (proposed.Version >= 2 && !planning) throw PlanningCapability(); ValidateProject(proposed, request.Document.Sections); }
        string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { id, request }, Json)));
        var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            // The same counter lock is used by database triggers. Receipts and aggregate commits are atomic.
            await db.Database.ExecuteSqlRawAsync("UPDATE DocumentSyncClocks SET Sequence=Sequence WHERE Id=1", ct);
            var receipt = await db.DocumentSyncOperations.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerUserId == owner && x.OperationId == request.OperationId, ct);
            if (receipt is not null)
            {
                if (receipt.RequestHash != hash) throw new DocumentSyncException(409, "operation_id_reused", "Use a new operation ID for a different request.");
                var replay = JsonSerializer.Deserialize<SyncMutationResult>(receipt.ResultJson, Json)!;
                await transaction.CommitAsync(ct);
                return replay;
            }
            var current = await db.DocumentSyncRecords.AsNoTracking().SingleOrDefaultAsync(x => x.DocumentId == id, ct);
            if (current is not null && current.OwnerUserId != owner)
                throw new DocumentSyncException(404, "document_not_found", "Document not found.");
            if (current is not null && (current.IsDeleted || current.Version != request.ExpectedVersion))
                throw new DocumentSyncException(409, current.IsDeleted ? "document_deleted" : "version_conflict", "Preserve local writing and fetch the current server version.", State(current));
            if (current is null && (request.ExpectedVersion is not null || request.Action != "upload"))
                throw new DocumentSyncException(404, "document_not_found", "Document not found; updates never recreate missing documents.");

            DocumentRecord document;
            if (current is null)
            {
                // A separate project mirrors existing standalone-document creation and avoids manuscript uniqueness collisions.
                var now = DateTimeOffset.UtcNow;
                var project = new ProjectRecord { Id = request.Document!.Project?.Id ?? Guid.NewGuid(), OwnerUserId = owner, Title = request.Document!.Project?.Title ?? request.Document!.Title, CreatedUtc = now, UpdatedUtc = now };
                var existingProject = await db.Projects.SingleOrDefaultAsync(p => p.Id == project.Id, ct);
                if (existingProject is not null)
                {
                    if (!multipleDocuments || request.Document.Project?.Version < 3 || existingProject.OwnerUserId != owner)
                        throw Invalid("Project identity is already in use.");
                    project = existingProject;
                }
                else db.Projects.Add(project);
                document = new DocumentRecord { Id = id, ProjectId = project.Id, OwnerUserId = owner, CreatedAt = now, UpdatedAt = now, DocumentKind = Enum.Parse<DocumentKind>(request.Document.Kind, true) };
                db.Documents.Add(document);
            }
            else document = await db.Documents.Include(x => x.Sections).ThenInclude(x => x.Pages).SingleAsync(x => x.Id == id && x.OwnerUserId == owner, ct);

            var projectRecord = db.Projects.Local.FirstOrDefault(p => p.Id == document.ProjectId)
                ?? await db.Projects.SingleAsync(p => p.Id == document.ProjectId && p.OwnerUserId == owner, ct);
            if (!multipleDocuments && await db.Documents.AnyAsync(d => d.ProjectId == document.ProjectId && d.Id != id, ct))
                throw MultipleDocumentCapability();
            if (!planning && (projectRecord.PlanningSyncEnabled || await db.DocumentSynopses.AnyAsync(s => s.DocumentId == id, ct) || await db.SceneAnnotations.AnyAsync(a => a.SceneNode!.ProjectId == document.ProjectId, ct))) throw PlanningCapability();
            bool structured = projectRecord.SyncEnabled || await db.ProjectNodes.IgnoreQueryFilters().AnyAsync(n => n.ProjectId == document.ProjectId, ct);
            if (structured && (!projects || (request.Action == "upload" && request.Document?.Project is null))) throw ProjectCapability();
            if (request.Document?.Project is { } incomingProject && incomingProject.Id != document.ProjectId)
                projectRecord = await MoveStandaloneToProjectAsync(document, projectRecord, incomingProject, owner, multipleDocuments, ct);
            if (document.DeletedAtUtc is not null && request.Action is "upload" or "rename")
                throw new DocumentSyncException(409, "document_trashed", "Restore the current server version before editing.", State(current!));
            switch (request.Action)
            {
                case "upload":
                    ValidateUploadContent(document, request.Document!);
                    await ApplyUploadAsync(document, request.Document!, ct);
                    if (request.Document!.Project is { } incoming)
                    {
                        await db.SaveChangesAsync(ct); // Materialize canonical section/page dependencies inside this transaction.
                        await ApplyProjectAsync(document, projectRecord, incoming, ct);
                    }
                    break;
                case "rename": document.Title = request.Title!; break;
                case "trash": document.DeletedAtUtc ??= DateTime.UtcNow; break;
                case "restore": document.DeletedAtUtc = null; document.IsArchived = false; document.ArchivedAt = null; break;
                case "delete":
                    if (document.DeletedAtUtc is null) throw new DocumentSyncException(409, "trash_required", "Move the document to Trash before permanently deleting it.", State(current!));
                    await deletion.DeleteOwnedDocumentInExistingTransactionAsync(id, owner, ct);
                    db.ChangeTracker.Clear();
                    break;
            }
            if (request.Action != "delete") { document.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); }
            var updated = await OwnedStateAsync(owner, id, ct);
            var committed = new SyncMutationResult(request.OperationId, State(updated),
                multipleDocuments ? projectRecord.MetadataRevision : null, multipleDocuments ? projectRecord.PrimaryDocumentId : null);
            db.DocumentSyncOperations.Add(new() { OwnerUserId = owner, OperationId = request.OperationId, RequestHash = hash, ResultJson = JsonSerializer.Serialize(committed, Json) });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return committed;
        });
        search.Enqueue(owner);
        return result;
    }

    private async Task ApplyUploadAsync(DocumentRecord document, SyncUpload upload, CancellationToken ct)
    {
        var existingSections = document.Sections.ToDictionary(x => x.Id);
        var existingPages = document.Sections.SelectMany(x => x.Pages).ToDictionary(x => x.Id);
        var sections = upload.Sections.Select(x => x.Id).ToHashSet();
        var pages = upload.Sections.SelectMany(x => x.Pages).Select(x => x.Id).ToHashSet();
        var removedSections = existingSections.Keys.Where(x => !sections.Contains(x)).ToArray();
        var removedPages = existingPages.Keys.Where(x => !pages.Contains(x)).ToArray();
        if ((removedSections.Length != 0 || removedPages.Length != 0) && (upload.Structure is null
            || removedSections.Any(x => !upload.Structure.RemovedSectionIds.Contains(x))
            || removedPages.Any(x => !upload.Structure.RemovedPageIds.Contains(x))))
            throw new DocumentSyncException(422, "structure_removal_not_supported", "The full server section/page set must be retained in a v1 upload.");
        if (await db.Sections.AnyAsync(x => sections.Contains(x.Id) && x.DocumentId != document.Id, ct)
            || await db.Pages.AnyAsync(x => pages.Contains(x.Id) && x.DocumentId != document.Id, ct))
            throw Invalid("Section/page identities are already in use.");
        // A device cannot delete metadata that is absent from the sync snapshot. Fail before mutating anything.
        if ((removedSections.Length != 0 || removedPages.Length != 0) && (removedSections.Any(id => existingSections[id].TranslationGroupId is not null)
            || await db.SectionNotes.AnyAsync(x => removedSections.Contains(x.SectionId), ct)
            || await db.SectionSceneCards.AnyAsync(x => removedSections.Contains(x.SectionId), ct)
            || await db.ProjectNodes.AnyAsync(x => x.LinkedSectionId != null && removedSections.Contains(x.LinkedSectionId.Value), ct)
            || await db.DocumentOutlineNodes.AnyAsync(x => x.LinkedSectionId != null && removedSections.Contains(x.LinkedSectionId.Value), ct)
            || await db.PageNotes.AnyAsync(x => removedPages.Contains(x.PageId), ct)
            || await db.PageAnnotations.AnyAsync(x => removedPages.Contains(x.PageId), ct)
            || await db.PageVersions.AnyAsync(x => removedPages.Contains(x.PageId), ct)
            || await db.PageQualityIssues.AnyAsync(x => removedPages.Contains(x.PageId), ct)
            || await db.PageQualityIssueDismissals.AnyAsync(x => removedPages.Contains(x.PageId), ct)))
            throw new DocumentSyncException(422, "structure_has_web_metadata", "Cloud content was retained because removed items have web-only notes, history, annotations, or project links. Restore the local items or manage them in the web app.");
        foreach (var id in removedPages)
        {
            if (ToDto(document).Sections.SelectMany(s => s.Pages).Single(p => p.Id == id).ContentFormat != "html")
                throw new DocumentSyncException(422, "structure_has_protected_content", "Cloud legacy content was retained. Restore it locally or manage it in the web app.");
            try { ValidateHtml(existingPages[id].Content); }
            catch (DocumentSyncException)
            { throw new DocumentSyncException(422, "structure_has_protected_content", "Cloud content was retained because this original cannot be restored through device sync. Restore it locally or manage it in the web app."); }
        }
        document.Title = upload.Title;
        document.DocumentKind = Enum.Parse<DocumentKind>(upload.Kind, true);
        document.LanguageCode = upload.LanguageCode;
        foreach (var section in upload.Sections)
        {
            if (!existingSections.TryGetValue(section.Id, out var target))
            {
                target = new() { Id = section.Id, DocumentId = document.Id, CreatedAt = DateTimeOffset.UtcNow };
                document.Sections.Add(target);
                db.Sections.Add(target);
            }
            target.Title = section.Title; target.OrderIndex = section.OrderIndex; target.NarrativePurpose = section.NarrativePurpose;
            target.LanguageCode = section.LanguageCode; target.UpdatedAt = DateTimeOffset.UtcNow;
            foreach (var page in section.Pages)
            {
                if (!existingPages.TryGetValue(page.Id, out var item))
                {
                    item = new() { Id = page.Id, DocumentId = document.Id, SectionId = target.Id, CreatedAt = DateTimeOffset.UtcNow };
                    target.Pages.Add(item);
                    db.Pages.Add(item);
                }
                else if (item.SectionId != target.Id)
                {
                    if (upload.Structure?.AllowPageMoves != true) throw Invalid("Moving existing pages requires an explicit structure change.");
                    existingSections[item.SectionId].Pages.Remove(item);
                    item.SectionId = target.Id;
                    item.Section = target;
                    target.Pages.Add(item);
                }
                item.Title = page.Title; item.Content = page.Content; item.OrderIndex = page.OrderIndex; item.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        foreach (var id in removedPages)
        {
            var page = existingPages[id];
            existingSections[page.SectionId].Pages.Remove(page);
            db.Pages.Remove(page);
        }
        foreach (var id in removedSections)
        {
            document.Sections.Remove(existingSections[id]);
            db.Sections.Remove(existingSections[id]);
        }
    }

    private static SyncDocument ToDto(DocumentRecord document) => new(document.Id, document.ProjectId, document.Title, document.LanguageCode,
        document.DocumentKind.ToString().ToLowerInvariant(), document.IsArchived, document.CreatedAt, document.UpdatedAt,
        document.Sections.OrderBy(x => x.OrderIndex).ThenBy(x => x.Id).Select(s => new SyncSection(s.Id, s.Title, s.OrderIndex, s.NarrativePurpose, s.LanguageCode,
            s.Pages.OrderBy(x => x.OrderIndex).ThenBy(x => x.Id).Select(p => new SyncPage(p.Id, p.Title, p.OrderIndex, p.Content,
                p.Content.TrimStart().StartsWith('{') ? "legacyJson" : p.Content.TrimStart().StartsWith('<') || p.Content.Length == 0 ? "html" : "legacyText")).ToArray())).ToArray());

    private static void Validate(Guid id, SyncMutation request)
    {
        if (id == Guid.Empty || request.OperationId == Guid.Empty || request.ExpectedVersion?.Length > 64
            || request.Action is not ("upload" or "rename" or "trash" or "restore" or "delete")) throw Invalid("Invalid identity, version, or action.");
        if (JsonSerializer.SerializeToUtf8Bytes(request, Json).Length > MaxRequestBytes) throw new DocumentSyncException(413, "payload_too_large", "Maximum request size including a PNG cover is 5 MiB.");
        var contentOnly = request;
        if (request.Document?.Project?.CoverImageUrl is { } cover && cover.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
            try { WriterApp.Shared.CoverStudioContract.ReadPng(cover); }
            catch (System.IO.InvalidDataException e) { throw Invalid(e.Message); }
            contentOnly = request with { Document = request.Document with { Project = request.Document.Project with { CoverImageUrl = null } } };
        }
        if (JsonSerializer.SerializeToUtf8Bytes(contentOnly, Json).Length > 2 * 1024 * 1024)
            throw new DocumentSyncException(413, "payload_too_large", "Writing and planning remain limited to 2 MiB, plus one validated PNG cover of at most 2 MiB.");
        if (request.Action == "rename" && (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)) throw Invalid("Title must contain 1–200 characters.");
        if (request.Action != "upload")
        { if (request.Document is not null) throw Invalid("Only upload accepts document content."); return; }
        var doc = request.Document;
        if (doc is not null && doc.Kind is not ("manuscript" or "notes" or "synopsis" or "outline" or "other")) throw Invalid("Unknown document kind.");
        if (doc?.Kind != "manuscript" && doc?.Project?.Nodes.Count > 0) throw Invalid("Supporting documents cannot contain manuscript structure.");
        if (doc is null || string.IsNullOrWhiteSpace(doc.Title) || doc.Title.Length > 200 || doc.LanguageCode?.Length > 35
            || doc.Sections is null || doc.Sections.Count is < 1 or > 100) throw Invalid("Invalid document metadata or section count.");
        var ids = new HashSet<Guid>(); var orders = new HashSet<int>(); int count = 0;
        foreach (var section in doc.Sections)
        {
            if (section is null || section.Id == Guid.Empty || !ids.Add(section.Id) || section.OrderIndex < 0 || !orders.Add(section.OrderIndex)
                || section.Title is null || section.Title.Length > 200 || section.NarrativePurpose?.Length > 4000 || section.LanguageCode?.Length > 35
                || section.Pages is null || section.Pages.Count < 1) throw Invalid("Invalid section metadata, ordering, or identities.");
            var pageOrders = new HashSet<int>();
            foreach (var page in section.Pages)
            {
                if (page is null || ++count > 1000 || page.Id == Guid.Empty || !ids.Add(page.Id) || page.OrderIndex < 0 || !pageOrders.Add(page.OrderIndex)
                    || page.Title is null || page.Title.Length > 200 || page.Content is null || page.Content.Length > 500_000 || page.ContentFormat is not ("html" or "legacyText" or "legacyJson"))
                    throw Invalid("Invalid page metadata, content format, ordering, or identities.");
            }
        }
        if (doc.Structure is { } structure)
        {
            if (structure.RemovedSectionIds is null || structure.RemovedPageIds is null
                || structure.RemovedSectionIds.Concat(structure.RemovedPageIds).Any(id => id == Guid.Empty || ids.Contains(id))
                || structure.RemovedSectionIds.Distinct().Count() != structure.RemovedSectionIds.Count
                || structure.RemovedPageIds.Distinct().Count() != structure.RemovedPageIds.Count)
                throw Invalid("Invalid structure removal identities.");
        }
    }

    private static void ValidateUploadContent(DocumentRecord existing, SyncUpload upload)
    {
        var originals = ToDto(existing).Sections.SelectMany(s => s.Pages).ToDictionary(p => p.Id);
        foreach (var page in upload.Sections.SelectMany(s => s.Pages))
        {
            // Ownership and expected version have already been checked in this transaction.
            // An unchanged original is carried verbatim, never sanitized or reparsed by a device.
            if (originals.TryGetValue(page.Id, out var original) && original.Content == page.Content && original.ContentFormat == page.ContentFormat)
                continue;
            if (page.ContentFormat != "html") throw Invalid("Convert edited legacy content to supported HTML before uploading.");
            ValidateHtml(page.Content);
        }
    }

    private static void ValidateHtml(string html)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument("");
        var fragment = parser.ParseFragment(html, document.Body!);
        var allowed = EditorContentContract.Tags;
        var pending = new Stack<(INode Node, int Depth)>(fragment.Select(n => (n, 0)));
        int nodes = 0;
        while (pending.TryPop(out var entry))
        {
            if (entry.Depth > 128 || ++nodes > 100_000) throw Invalid("HTML is too deeply nested or complex.");
            INode node = entry.Node;
            if (node is IElement element)
            {
                if (!allowed.Contains(element.LocalName)) throw Invalid("Unsupported HTML; sync does not silently strip content.");
                foreach (var attr in element.Attributes)
                {
                    bool safe = EditorContentContract.AllowedAttribute(element.LocalName, attr.Name, attr.Value);
                    if (!safe) throw Invalid("Unsupported HTML attribute or unsafe link.");
                }
            }
            foreach (var child in node.ChildNodes) pending.Push((child, entry.Depth + 1));
        }
    }
}
