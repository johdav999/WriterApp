using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Shared;

namespace WriterApp.Controllers;

[ApiController, Authorize, Route("api/documents/{documentId:guid}/structured-translations")]
public sealed class WebTranslationsController(AppDbContext db, IUserIdResolver users) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private string Owner => users.ResolveUserId(User);
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Json)));
    private sealed record Recovery(WebTranslationSource Source, IReadOnlyList<NodeCopy> Nodes);
    private sealed record NodeCopy(Guid Id, Guid? ParentId, ProjectNodeType Type, string Title, int Order,
        Guid? SectionId, string? Metadata);

    [NonAction]
    public async Task ValidateGeneration(WebTranslationSource source, TranslationStructure original, CancellationToken ct)
    {
        var current = await Capture(source.DocumentId,source.Scope,source.Sections.FirstOrDefault()?.Id,ct);
        Fresh(current,source);
        ValidateMapping(current,original,original);
    }

    [HttpGet("source")]
    public Task<IActionResult> Source(Guid documentId, string scope, Guid? sectionId, CancellationToken ct, string? purpose = null) => Run(async () => {
        if (purpose is not (null or "consistency")) throw new InvalidDataException("Unsupported structured-source purpose.");
        return (object)await Capture(documentId, scope, sectionId, ct, purpose == "consistency");
    });

    [HttpGet("operations")]
    public Task<IActionResult> Operations(Guid documentId, CancellationToken ct) => Run(async () => {
        await Owned(documentId, ct);
        var rows = await db.WebTranslationOperations.AsNoTracking().Where(x => x.OwnerUserId == Owner && x.DocumentId == documentId).ToListAsync(ct);
        return (object)rows.OrderByDescending(x => x.CreatedAt).Take(100).Select(x => JsonSerializer.Deserialize<WebTranslationReceipt>(x.ReceiptJson, Json)!).ToArray();
    });

    [HttpPost("approve"), RequestSizeLimit(4 * 1024 * 1024)]
    public Task<IActionResult> Approve(Guid documentId, WebTranslationApproval request, CancellationToken ct) => Transaction(async () => {
        if (request.Source is null || request.Original is null || request.Translated is null || request.Source.Sections is null
            || request.OperationId == Guid.Empty || request.ProposalId == Guid.Empty || request.Source.DocumentId != documentId
            || request.Source.Version != 1 || request.Source.Sections.Count == 0
            || request.SourceLanguage != "auto" && !WriterApp.Shared.Localization.TranslationLanguages.All.Any(l => l.Code == request.SourceLanguage)
            || request.Mode != "replace" && request.Mode != "duplicate-" + request.Source.Scope
            || request.IsWriting && (request.Mode != "replace" || request.Source.Scope != "section"))
            throw new InvalidDataException("Invalid translation approval or application mode.");
        string hash = Hash(request);
        var existing = await db.WebTranslationOperations.SingleOrDefaultAsync(x => x.OwnerUserId == Owner && x.OperationId == request.OperationId, ct);
        if (existing is not null) {
            if (existing.DocumentId != documentId || existing.RequestHash != hash) throw new DocumentSyncException(409,"operation_reused","The operation ID already belongs to a different approval.");
            return (object)JsonSerializer.Deserialize<WebTranslationReceipt>(existing.ReceiptJson, Json)!;
        }
        if(await db.WebTranslationOperations.AnyAsync(x => x.OwnerUserId==Owner && x.ProposalId==request.ProposalId,ct))
            throw new DocumentSyncException(409,"proposal_already_approved","This proposal already has a durable approval. Reload recovery and use that operation; it cannot create another copy.");
        var current = await Capture(documentId, request.Source.Scope, request.Source.Sections.First().Id, ct);
        Fresh(current, request.Source);
        TranslationStructures.Validate(request.Original);
        _ = TranslationStructures.Result(TranslationStructures.Serialize(request.Translated), request.Original);
        ValidateMapping(current, request.Original, request.Translated);
        var history = await db.AiActionHistoryEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ProposalId && x.OwnerUserId == Owner && x.DocumentId == documentId, ct)
            ?? throw new InvalidDataException("Owned translation proposal not found. Generate a new checked preview.");
        var proposal = JsonSerializer.Deserialize<AiActionExecuteResponseDto>(history.ResultJson, Json);
        var generation = JsonSerializer.Deserialize<AiActionExecuteRequestDto>(history.RequestJson, Json);
        if(proposal is null || generation is null)throw new InvalidDataException("Missing checked proposal evidence. Generate again.");
        var checkedResult=request.IsWriting ? WritingTranslation(proposal.ProposedText ?? "",request.Original) : TranslationStructures.Result(proposal.ProposedText ?? "",request.Original);
        WebAiSources.RequireTime(history.CreatedAt);
        if (proposal.SourceDocumentVersion != generation.ExpectedDocumentVersion
            || (request.IsWriting ? !WritingOutline.Consumes(proposal.ActionKey) || generation.WebSource is null : proposal.ActionKey != "translate." + current.Scope)
            || TranslationStructures.Serialize(checkedResult) != TranslationStructures.Serialize(request.Translated)
            || generation.Parameters?.GetValueOrDefault(request.IsWriting ? WritingActions.Parameter : TranslationStructures.Parameter)?.ToString() !=
                (request.IsWriting ? WritingActions.Serialize(new(1,documentId,request.Original.Sections.Single().Id,request.Original.Sections.Single().Pages)) : TranslationStructures.Serialize(request.Original))
            || !request.IsWriting && generation.Parameters.GetValueOrDefault("source_language")?.ToString() != request.SourceLanguage
            || DateTimeOffset.UtcNow - history.CreatedAt > TimeSpan.FromHours(1))
            throw new InvalidDataException("The proposal is expired or does not match its saved checked source/result.");
        var recoverySource = await Capture(documentId,"document",null,ct);
        var nodes = await db.ProjectNodes.AsNoTracking().Where(n => n.DocumentId == documentId).OrderBy(n => n.Id).ToListAsync(ct);
        var receipt = new WebTranslationReceipt(request.OperationId, request.ProposalId, documentId,"Approved",null,null,Guid.NewGuid());
        db.WebTranslationOperations.Add(new() { OwnerUserId=Owner, OperationId=request.OperationId, ProposalId=request.ProposalId, DocumentId=documentId,
            RequestHash=hash, ApprovalJson=JsonSerializer.Serialize(request,Json), RecoveryJson=JsonSerializer.Serialize(new Recovery(recoverySource,
                nodes.Select(n => new NodeCopy(n.Id,n.ParentId,n.NodeType,n.Title,n.OrderIndex,n.LinkedSectionId,n.MetadataJson)).ToArray()),Json),
            ReceiptJson=JsonSerializer.Serialize(receipt,Json), CreatedAt=DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        return (object)receipt;
    },ct);

    [HttpPost("operations/{operationId:guid}/commit")]
    public Task<IActionResult> Commit(Guid documentId, Guid operationId, CancellationToken ct) => Transaction(async () => {
        var row = await Operation(documentId, operationId, ct);
        var receipt = JsonSerializer.Deserialize<WebTranslationReceipt>(row.ReceiptJson,Json)!;
        if (receipt.State == "Committed") return (object)receipt;
        var approval = JsonSerializer.Deserialize<WebTranslationApproval>(row.ApprovalJson,Json)!;
        var current = await Capture(documentId,approval.Source.Scope,approval.Source.Sections.First().Id,ct);
        Fresh(current,approval.Source);
        var html = ValidateMapping(current, approval.Original, approval.Translated);
        var history=await db.WebAiHistoryOperations.SingleOrDefaultAsync(h=>h.OwnerUserId==Owner && h.OperationId==operationId,ct);
        WebRecoverySnapshot? before=null;
        if(history is not null) {
            var intent=WebAiHistoryOperations.Intent(history);
            if(intent.TargetKind!="Aggregate" || intent.AggregateOperationId!=operationId || intent.ProposalId!=row.ProposalId
                || intent.Source.DocumentId!=documentId || history.CommittedAt!=null || intent.Sequence!=1)
                throw new InvalidDataException("The prepared aggregate history does not match this approval.");
            await new WebAiSourceService(db,Owner).Require(intent.Source,ct);
            if(approval.Mode=="replace")before=await new WebAggregateRecovery(db).Capture(current,ct);
        }
        Guid resultDocument = documentId; Guid? resultSection = null;
        if (approval.Mode == "replace") {
            var pages = await db.Pages.Where(p => p.DocumentId == documentId).ToListAsync(ct);
            foreach (var page in pages.Where(p => html.ContainsKey(p.Id))) { page.Content=html[page.Id]; page.UpdatedAt=DateTimeOffset.UtcNow; }
            var sections = await db.Sections.Where(s => s.DocumentId == documentId).ToListAsync(ct);
            foreach (var section in sections.Where(s => current.Sections.Any(x => x.Id == s.Id))) {
                if(!approval.IsWriting) section.LanguageCode=approval.Translated.TargetLanguage; section.UpdatedAt=DateTimeOffset.UtcNow;
            }
            var document=await Owned(documentId,ct);
            if(current.Scope=="document") document.LanguageCode=approval.Translated.TargetLanguage;
            document.UpdatedAt=DateTimeOffset.UtcNow;
            await RefreshMirrors(documentId,html,approval.Translated.TargetLanguage,ct,approval.IsWriting);
        } else {
            var recovery = JsonSerializer.Deserialize<Recovery>(row.RecoveryJson,Json)!;
            var copy = await Copy(current,recovery.Nodes,html,approval.Translated.TargetLanguage,
                current.Scope=="document" ? Guid.NewGuid() : documentId,ct);
            resultDocument=copy.Document;resultSection=copy.Section;
        }
        // Raw rich snapshots, terminal receipt and all writing share this transaction.
        receipt=receipt with { State="Committed", ResultDocumentId=resultDocument, ResultSectionId=resultSection };
        row.ReceiptJson=JsonSerializer.Serialize(receipt,Json);
        await db.SaveChangesAsync(ct);
        if(history is not null) {
            history.RecoveryJson=before is null?null:await new WebAggregateRecovery(db).Complete(before,ct) is { } snapshot?WebPlanningRecovery.Serialize(snapshot):null;
            history.CommittedAt=DateTimeOffset.UtcNow;history.SavedResponseJson=row.ReceiptJson;
            await db.SaveChangesAsync(ct);
        }
        return (object)receipt;
    },ct);

    [HttpPost("operations/{operationId:guid}/original")]
    public Task<IActionResult> Original(Guid documentId, Guid operationId, CancellationToken ct) => Transaction(async () => {
        var row=await Operation(documentId,operationId,ct);
        var receipt=JsonSerializer.Deserialize<WebTranslationReceipt>(row.ReceiptJson,Json)!;
        var existing=await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id==receipt.OriginalCopyId,ct);
        if(existing is not null) {
            if(existing.OwnerUserId!=Owner) throw new InvalidDataException("Recovery identity belongs to another account.");
            return (object)new { DocumentId=existing.Id };
        }
        if(await db.DocumentSyncRecords.AnyAsync(x => x.DocumentId==receipt.OriginalCopyId,ct))
            throw new DocumentSyncException(409,"recovery_copy_deleted","The reserved original copy was deleted or is unavailable. This retry cannot recreate it; the original approval evidence is retained.");
        var recovery=JsonSerializer.Deserialize<Recovery>(row.RecoveryJson,Json)!;
        await Copy(recovery.Source,recovery.Nodes,recovery.Source.Sections.SelectMany(s => s.Pages).ToDictionary(p => p.Id,p => p.Content),
            recovery.Source.LanguageCode,receipt.OriginalCopyId,ct,true);
        await db.SaveChangesAsync(ct);
        return (object)new { DocumentId=receipt.OriginalCopyId };
    },ct);

    private async Task<WebTranslationOperation> Operation(Guid id,Guid operation,CancellationToken ct) =>
        await db.WebTranslationOperations.SingleOrDefaultAsync(x => x.OwnerUserId==Owner && x.DocumentId==id && x.OperationId==operation,ct)
        ?? throw new DocumentSyncException(404,"operation_missing","Approved translation not found for this account/document.");
    private async Task<DocumentRecord> Owned(Guid id,CancellationToken ct) =>
        await db.Documents.SingleOrDefaultAsync(d => d.Id==id && d.OwnerUserId==Owner && d.DeletedAtUtc==null && !d.IsArchived,ct)
        ?? throw new DocumentSyncException(404,"document_missing","Owned active document not found.");
    private async Task<WebTranslationSource> Capture(Guid id,string scope,Guid? sectionId,CancellationToken ct,bool annotatedRead = false)
    {
        var doc=await Owned(id,ct);
        // Capability discovery must fail before provider generation on an unmigrated backend.
        _=await db.WebTranslationOperations.AsNoTracking().Select(x => x.OperationId).Take(1).ToListAsync(ct);
        if(!await db.Projects.AnyAsync(p => p.Id==doc.ProjectId && p.OwnerUserId==Owner,ct))
            throw new DocumentSyncException(404,"project_missing","Owned project not found.");
        var state=await db.DocumentSyncRecords.AsNoTracking().SingleOrDefaultAsync(x => x.DocumentId==id && x.OwnerUserId==Owner && !x.IsDeleted && !x.IsTrashed,ct)
            ?? throw new DocumentSyncException(409,"capability_missing","Checked translation requires the current document-sync schema and translation migrations.");
        if(scope is not ("section" or "document")) throw new InvalidDataException("Choose section or document translation.");
        var sections=await db.Sections.AsNoTracking().Where(s => s.DocumentId==id).OrderBy(s => s.OrderIndex).ThenBy(s => s.Id).ToListAsync(ct);
        var pages=await db.Pages.AsNoTracking().Where(p => p.DocumentId==id).OrderBy(p => p.OrderIndex).ThenBy(p => p.Id).ToListAsync(ct);
        var nodes=await db.ProjectNodes.AsNoTracking().Where(n => n.DocumentId==id).OrderBy(n => n.Id).Take(5001).ToListAsync(ct);
        if(nodes.Count>5000 || nodes.Sum(n => (long)(n.MetadataJson?.Length ?? 0)+n.Title.Length)>2_000_000
            || nodes.Any(n => n.ProjectId!=doc.ProjectId))
            throw new InvalidDataException("Planning is oversized or not associated with this owned project. Preserve the source and resolve its structure before translating.");
        var selected=scope=="document" ? sections : sections.Where(s => s.Id==sectionId).ToList();
        if(selected.Count==0 || pages.Sum(p => (long)p.Content.Length)>2_000_000 || pages.Count>1000 || sections.Count>100)
            throw new InvalidDataException("Missing or oversized translation source. Translate a smaller document.");
        var selectedIds=selected.Select(s => s.Id).ToArray();
        if(!annotatedRead && (await db.PageAnnotations.AnyAsync(a => pages.Select(p => p.Id).Contains(a.PageId) && selectedIds.Contains(a.Page!.SectionId),ct)
            || await db.SceneAnnotations.AnyAsync(a => a.SceneNode!.DocumentId==id && selectedIds.Contains(a.SceneNode.LinkedSectionId!.Value),ct)))
            throw new InvalidDataException("Whole-scope translation cannot preserve anchored annotations. Use selection translation.");
        WebTranslationSection Section(SectionRecord s) => new(s.Id,s.Title,s.NarrativePurpose,s.OrderIndex,s.LanguageCode,s.TranslationGroupId,
            pages.Where(p => p.SectionId==s.Id).Select(p => new WebTranslationPage(p.Id,p.Title,p.OrderIndex,p.Content)).ToArray());
        string fingerprint=Hash(new { doc.Id,doc.ProjectId,doc.Title,doc.LanguageCode,doc.TranslationGroupId,
            Sections=sections.Select(Section),Nodes=nodes.Select(n => new { n.Id,n.ParentId,n.LinkedSectionId,n.Title,n.MetadataJson,n.OrderIndex }) });
        return new(1,Hash(Owner),id,doc.ProjectId,doc.Title,doc.LanguageCode,state.Version,fingerprint,scope,selected.Select(Section).ToArray());
    }
    private static void Fresh(WebTranslationSource current,WebTranslationSource source) {
        if(JsonSerializer.Serialize(current with { DocumentVersion="ack" },Json)!=JsonSerializer.Serialize(source with { DocumentVersion="ack" },Json))
            throw new DocumentSyncException(409,"source_changed","Writing, planning or account changed. Preserve this preview and request a new translation.");
    }
    private static TranslationStructure WritingTranslation(string text,TranslationStructure original) {
        var source=new WritingStructure(1,original.DocumentId,original.Sections.Single().Id,original.Sections.Single().Pages);
        var result=WritingActions.Result(text,source);
        return original with { Sections=[new(result.SectionId,result.Pages)] };
    }
    private static Dictionary<Guid,string> ValidateMapping(WebTranslationSource source,TranslationStructure original,TranslationStructure translated) {
        if(original.DocumentId!=source.DocumentId || original.Scope!=source.Scope
            || !source.Sections.Select(s => s.Id).SequenceEqual(original.Sections.Select(s => s.Id))) throw new InvalidDataException("Wrong translation target.");
        var result=new Dictionary<Guid,string>();
        for(int s=0;s<source.Sections.Count;s++) {
            if(!source.Sections[s].Pages.Select(p => p.Id).SequenceEqual(original.Sections[s].Pages.Select(p => p.Id))) throw new InvalidDataException("Incomplete page mapping.");
            for(int p=0;p<source.Sections[s].Pages.Count;p++) result.Add(source.Sections[s].Pages[p].Id,
                WebTranslationHtml.Map(source.Sections[s].Pages[p].Content,original.Sections[s].Pages[p],translated.Sections[s].Pages[p]));
        }
        return result;
    }
    private async Task<(Guid Document,Guid? Section)> Copy(WebTranslationSource source,IReadOnlyList<NodeCopy> nodes,
        IReadOnlyDictionary<Guid,string> html,string? language,Guid target,CancellationToken ct,bool recovery=false)
    {
        var now=DateTimeOffset.UtcNow;
        if(!await db.Projects.AnyAsync(p => p.Id==source.ProjectId && p.OwnerUserId==Owner,ct))
            throw new DocumentSyncException(404,"project_missing","The original owned project is unavailable. Keep the retained recovery evidence.");
        bool newDocument=target!=source.DocumentId;
        if(!recovery) {
            if(newDocument) {
                var originalDocument=await Owned(source.DocumentId,ct);
                originalDocument.TranslationGroupId ??= source.DocumentId;
            } else {
                foreach(var section in await db.Sections.Where(s => s.DocumentId==source.DocumentId).ToListAsync(ct))
                    if(source.Sections.Any(s => s.Id==section.Id)) section.TranslationGroupId ??= section.Id;
            }
        }
        if(newDocument) db.Documents.Add(new() { Id=target,ProjectId=source.ProjectId,OwnerUserId=Owner,DocumentKind=DocumentKind.Other,
            Title=source.Title+(recovery ? " (translation original)" : " ("+language+" translation)"),LanguageCode=language,
            TranslationGroupId=recovery ? null : (await Owned(source.DocumentId,ct)).TranslationGroupId ?? source.DocumentId,CreatedAt=now,UpdatedAt=now });
        var sectionMap=source.Sections.ToDictionary(s => s.Id,_ => Guid.NewGuid());
        int next=newDocument ? 0 : await db.Sections.Where(s => s.DocumentId==target).MaxAsync(s => s.OrderIndex,ct)+1;
        foreach(var section in source.Sections) {
            db.Sections.Add(new() { Id=sectionMap[section.Id],DocumentId=target,Title=section.Title+(newDocument ? "" : " ("+language+" translation)"),
                NarrativePurpose=section.NarrativePurpose,LanguageCode=recovery ? section.LanguageCode : language,
                TranslationGroupId=recovery ? section.TranslationGroupId : section.TranslationGroupId ?? section.Id,OrderIndex=next++,CreatedAt=now,UpdatedAt=now });
            foreach(var page in section.Pages) db.Pages.Add(new() { Id=Guid.NewGuid(),DocumentId=target,SectionId=sectionMap[section.Id],Title=page.Title,
                Content=html[page.Id],OrderIndex=page.OrderIndex,CreatedAt=now,UpdatedAt=now });
        }
        var selected=nodes.Where(n => newDocument || n.SectionId is { } sid && sectionMap.ContainsKey(sid)).ToArray();
        var nodeMap=selected.ToDictionary(n => n.Id,_ => Guid.NewGuid());
        foreach(var node in selected) db.ProjectNodes.Add(new() { Id=nodeMap[node.Id],DocumentId=target,ProjectId=source.ProjectId,
            ParentId=node.ParentId is { } parent && nodeMap.TryGetValue(parent,out var mapped) ? mapped : newDocument ? null : node.ParentId,
            NodeType=node.Type,Title=node.Title,OrderIndex=node.Order,MetadataJson=node.Metadata,
            LinkedSectionId=node.SectionId is { } linked && sectionMap.TryGetValue(linked,out var section) ? section : null,UpdatedUtc=now });
        await db.SaveChangesAsync(ct);
        await RefreshMirrors(target,new Dictionary<Guid,string>(),language,ct);
        return (target,newDocument ? null : sectionMap.Values.Single());
    }
    private async Task RefreshMirrors(Guid id,IReadOnlyDictionary<Guid,string> changed,string? language,CancellationToken ct,bool preserveLanguage=false) {
        var pages=await db.Pages.Where(p => p.DocumentId==id).OrderBy(p => p.OrderIndex).ToListAsync(ct);
        var nodes=await db.ProjectNodes.Where(n => n.DocumentId==id && n.LinkedSectionId!=null).ToListAsync(ct);
        foreach(var node in nodes.Where(n => changed.Count==0 || pages.Any(p => p.SectionId==n.LinkedSectionId && changed.ContainsKey(p.Id)))) {
            var mirror=await db.SceneContents.SingleOrDefaultAsync(s => s.SceneNodeId==node.Id,ct);
            if(mirror is null) { mirror=new() { SceneNodeId=node.Id };db.SceneContents.Add(mirror); }
            mirror.ContentJson=string.Join("\n\n",pages.Where(p => p.SectionId==node.LinkedSectionId).Select(p => p.Content));
            if(!preserveLanguage)mirror.LanguageCode=language;mirror.UpdatedAtUtc=DateTimeOffset.UtcNow;
        }
    }
    private Task<IActionResult> Transaction(Func<Task<object>> action,CancellationToken ct) => Run(async () =>
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            db.ChangeTracker.Clear();
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            await db.Database.ExecuteSqlRawAsync("UPDATE DocumentSyncClocks SET Sequence=Sequence WHERE Id=1",ct);
            var result=await action();await tx.CommitAsync(ct);return result;
        }));
    private async Task<IActionResult> Run(Func<Task<object>> action) {
        Response.Headers.CacheControl="no-store";
        try { return Ok(await action()); }
        catch(InvalidOperationException e) { return Conflict(new { message=e.Message }); }
        catch(DocumentSyncException e) { return StatusCode(e.Status,new { message=e.Message }); }
        catch(Exception e) when(e is InvalidDataException or JsonException or ArgumentException) { return BadRequest(new { message=e.Message }); }
        catch(Exception e) when(e is DbUpdateException or DbException) { return StatusCode(503,new { message="Translation storage is unavailable. Confirm the matching translation migrations. If a save was approved, reconcile its unchanged operation ID before retrying." }); }
    }
}
