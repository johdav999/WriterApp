using System.Text.Json;
using Microsoft.JSInterop;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using WriterApp.Shared.Brainstorming;
using WriterApp.Shared.Canon;

namespace WriterApp.Device.Shared.Services;

public sealed class DeviceBrainstormingTextProjector(IJSRuntime js) : IBrainstormingTextProjector
{
    public async Task<BrainstormingProjectedPage> ProjectAsync(BrainstormingStoryPage page, CancellationToken ct)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", ct, "./_content/WriterApp.Device.Shared/editor/device-editor.js");
        var text = await module.InvokeAsync<string>("qualityPlainText", ct, page.Content, page.Format);
        return new(page.Id, BrainstormingValidation.Fingerprint(page.Content), text);
    }
}

public sealed class DeviceBrainstormingContext(LocalDocumentRepository documents, DeviceBibleService canon, DeviceAccountService account,
    DeviceBrainstormingSessions conversations, DeviceBrainstormingTextProjector projector)
{
    // Called only after the workspace's destination-specific Save, sync & prepare confirmation.
    public async Task SynchronizeForReviewAsync(LocalEditorSession editor, Func<Task<bool>> save, DeviceSyncEngine sync)
    {
        var generation = account.Generation;
        var writing = DeviceSyncMapping.Fingerprint(editor.Snapshot());
        // An upload acknowledgment may be newer than the open editor's metadata.
        await editor.RefreshSyncMetadataAsync();
        if (editor.IsDirty && !await save())
            throw new InvalidDataException("Saving failed. Your writing and message are retained. Retry Save before preparing context.");
        await editor.RefreshSyncMetadataAsync();
        if (editor.Document.ServerDocumentId is null)
            throw new InvalidDataException("Your writing is saved on this device. Enable cloud sync for this manuscript in its sync controls, then prepare context again.");
        if (editor.Document.SyncState != LocalSyncState.Synced)
        {
            await sync.SyncAsync();
            await editor.RefreshSyncMetadataAsync();
            var status = sync.Status(editor.Document.DocumentId);
            if (status?.Conflict == true || status?.Deleted == true)
                throw new InvalidDataException("Cloud writing needs conflict or deletion review in the sync controls. Your local writing and message are retained.");
            if (editor.Document.SyncState != LocalSyncState.Synced)
                throw new InvalidDataException(status?.Error ?? sync.LastError ?? "Writing is saved on this device but not synchronized. Connect and retry preparation; your message is retained.");
        }
        if (generation != account.Generation || editor.IsDirty || writing != DeviceSyncMapping.Fingerprint(editor.Snapshot()))
            throw new InvalidDataException("Writing or account changed while preparing context. Your edits and message are retained; prepare again.");
    }

    public async Task<BrainstormingPreparation> PrepareServerAsync(LocalEditorSession editor,BrainstormingTargetQuery query,IReadOnlyList<BrainstormingContextChoice> choices,
        IReadOnlyList<Guid> supporting,int budget,BrainstormingScope scope,CancellationToken ct=default) {
        if(editor.IsDirty || editor.Document.SyncState!=LocalSyncState.Synced)throw new InvalidDataException("Save and synchronize pending writing before reviewing server context. Local text is retained.");
        var saved=editor.Snapshot();var generation=account.Generation;
        var cache=await conversations.Sessions.ReadAsync(scope,ct)??await conversations.Sessions.CreateAsync(scope,ct:ct);
        cache=await conversations.MapAsync(scope,cache.Mapping?.Server.SessionId??Guid.NewGuid(),ct);
        var all=new List<LocalDocument>{saved};foreach(var id in supporting)all.Add(await documents.LoadAsync(id,ct)??throw new InvalidDataException("Supporting document unavailable."));
        if(all.Any(d=>d.DeletedAtUtc is not null || d.SyncState!=LocalSyncState.Synced || d.ServerDocumentId is null))throw new InvalidDataException("Synchronize every selected supporting document first.");
        var ids=new BrainstormingIdentityMap(all.ToDictionary(d=>d.DocumentId,d=>d.ServerDocumentId!.Value),all.SelectMany(d=>d.Sections).ToDictionary(s=>s.SectionId,s=>s.ServerSectionId??throw new InvalidDataException("Map every section first.")),
            all.SelectMany(d=>d.Sections).SelectMany(s=>s.Pages).ToDictionary(p=>p.PageId,p=>p.ServerPageId??throw new InvalidDataException("Map every page first.")),
            all.SelectMany(d=>d.Project?.Nodes??[]).Where(n=>n.DeletionId is null).ToDictionary(n=>n.NodeId,n=>n.ServerNodeId??throw new InvalidDataException("Map every outline node first.")));
        Guid? Map(Guid? id,IReadOnlyDictionary<Guid,Guid> map)=>id is null?null:map.TryGetValue(id.Value,out var mapped)?mapped:throw new InvalidDataException("Destination is not synchronized.");
        if(choices.Count!=0 && cache.Session.Contexts.LastOrDefault()?.Policy?.Limitation.Length!=0)throw new InvalidDataException("Clear local source choices, review synchronized server context, then select sources or review summaries of those checked versions.");
        var result=await conversations.Conversation.PrepareAsync(new(1,cache.Mapping!.Server,query with {ChapterId=Map(query.ChapterId,ids.Nodes),SectionId=Map(query.SectionId,ids.Sections),SceneId=Map(query.SceneId,ids.Nodes)},choices,supporting.Select(id=>ids.Documents[id]).ToArray(),budget),ct);
        foreach(var remote in result.Documents) {
            var local=all.Single(d=>d.ServerDocumentId==remote.Outline.DocumentId);
            var expectedSynopsis=BrainstormingPersistence.Json(LocalBrainstormingPlanning.Synopsis(local));
            if(remote.References.SingleOrDefault(s=>s.Kind==BrainstormingSourceKind.Synopsis && s.Availability==BrainstormingSourceAvailability.Available)?.Text is { } synopsis && synopsis!=expectedSynopsis
                || LocalBrainstormingPlanning.Synopsis(local)!=new WriterApp.Shared.Sync.SyncSynopsis() && !remote.References.Any(s=>s.Kind==BrainstormingSourceKind.Synopsis && s.Text==expectedSynopsis))
                throw new InvalidDataException("Local and server authored planning differ. Synchronize and explicitly review current notes; no older cloud synopsis was substituted.");
            if(local.ServerVersion!=remote.Outline.DocumentVersion || local.Sections.Count!=remote.Outline.Sections.Count || local.Sections.Sum(s=>s.Pages.Count)!=remote.Pages.Count)throw new InvalidDataException("Local and server writing differ. Synchronize and explicitly review again.");
            foreach(var section in local.Sections) {
                if(!remote.Outline.Sections.Any(s=>s.Id==section.ServerSectionId && s.Order==section.OrderIndex && s.Title==section.Title))throw new InvalidDataException("Local section structure differs from the checked server.");
                foreach(var page in section.Pages)if(page.ContentFormat!=LocalContentFormat.Html || !remote.Pages.Any(p=>p.Id==page.ServerPageId && p.SectionId==section.ServerSectionId && p.Content==page.Content && p.Order==page.OrderIndex && p.Title==page.Title))throw new InvalidDataException("Local writing differs from the checked server. No older cloud text was substituted.");
            }
            var latest=await documents.LoadAsync(local.DocumentId,ct);
            if(latest is null || DeviceSyncMapping.Fingerprint(latest)!=DeviceSyncMapping.Fingerprint(local))throw new InvalidDataException("Local source changed during server review.");
        }
        if(generation!=account.Generation || DeviceSyncMapping.Fingerprint(editor.Snapshot())!=DeviceSyncMapping.Fingerprint(saved))throw new OperationCanceledException("Writing/account changed during server review.");
        if(result.Preparation.Context is not { } context || result.Preparation.Resolution.Target is not { } target)return result.Preparation;
        var temporary=BrainstormingPersistence.Empty(cache.Mapping.Server) with {Contexts=[context],Targets=[target]};var mapped=ids.Reverse().Convert(temporary,scope);
        var resolution=result.Preparation.Resolution with {Target=mapped.Targets[0],PrecedingSectionIds=result.Preparation.Resolution.PrecedingSectionIds.Select(id=>ids.Reverse().Sections[id]).ToArray(),TargetSectionIds=result.Preparation.Resolution.TargetSectionIds.Select(id=>ids.Reverse().Sections[id]).ToArray()};
        return result.Preparation with {Context=mapped.Contexts[0],Resolution=resolution};
    }
    public static BrainstormingStorySnapshot Capture(LocalDocument saved, LocalDocument pending)
    {
        if (saved.DocumentId != pending.DocumentId || saved.DeletedAtUtc is not null || pending.DeletedAtUtc is not null
            || pending.Project is { } project && project.ManuscriptId != pending.DocumentId) throw new InvalidDataException("The active local manuscript is unavailable or belongs to another outline.");
        bool dirty = DeviceSyncMapping.Fingerprint(saved) != DeviceSyncMapping.Fingerprint(pending);
        var outline = WritingOutline.Create(pending.DocumentId, pending.Project?.ProjectId, pending.LocalRevision.ToString(),
            pending.Sections.Select(s => new WritingOutlineSection(s.SectionId, s.OrderIndex, s.Title)),
            (pending.Project?.Nodes ?? []).Where(n => n.DeletionId is null).Select(n => new WritingOutlineNode(n.NodeId, n.ParentId, n.OrderIndex, n.NodeType.ToLowerInvariant(), n.Title, n.SectionId)));
        var pages = pending.Sections.SelectMany(s => s.Pages.Select(p => new BrainstormingStoryPage(p.PageId, s.SectionId, p.OrderIndex, p.Title, p.Content, p.ContentFormat.ToString(),
            pending.LocalRevision.ToString(), saved.Sections.SelectMany(x => x.Pages).Any(x => x.PageId == p.PageId && x.Content == p.Content && x.ContentFormat == p.ContentFormat)
                ? pending.SyncState == LocalSyncState.Synced ? BrainstormingFreshness.Saved : BrainstormingFreshness.LocalAheadOfServer : BrainstormingFreshness.PendingEditorChanges))).ToArray();
        var refs = new List<BrainstormingStoryReference>();
        void Add(string key, BrainstormingSourceKind kind, string title, object value, Guid? node = null) => refs.Add(new(BrainstormingStorySources.Identity(pending.DocumentId, key), pending.DocumentId, kind, title,
            value is string str ? str : JsonSerializer.Serialize(value, BrainstormingValidation.Json), pending.LocalRevision.ToString(), dirty ? BrainstormingFreshness.PendingEditorChanges : BrainstormingFreshness.Saved,
            BrainstormingSourceNature.PlanningIntent, NodeId: node));
        if (pending.Project?.Synopsis is { } synopsis) Add("synopsis", BrainstormingSourceKind.Synopsis, "Synopsis", synopsis);
        else if(pending.ExtensionData?.ContainsKey("authoredSynopsis")==true) Add("synopsis",BrainstormingSourceKind.Synopsis,"Synopsis",LocalBrainstormingPlanning.Synopsis(pending));
        else refs.Add(new(BrainstormingStorySources.Identity(pending.DocumentId,"synopsis"),pending.DocumentId,BrainstormingSourceKind.Synopsis,"Synopsis","",pending.LocalRevision.ToString(),BrainstormingFreshness.Saved,BrainstormingSourceNature.PlanningIntent,BrainstormingSourceAvailability.Missing));
        foreach (var node in (pending.Project?.Nodes ?? []).Where(n => n.DeletionId is null)) {
            if (node.Card is { } card) Add("card:" + node.NodeId, BrainstormingSourceKind.Scene, "Scene card", card, node.NodeId);
            if (!string.IsNullOrEmpty(node.Notes)) Add("note:" + node.NodeId, BrainstormingSourceKind.StoryNote, "Notes: " + node.Title[..Math.Min(180, node.Title.Length)], node.Notes, node.NodeId);
        }
        foreach (var kind in new[] { BrainstormingSourceKind.Character, BrainstormingSourceKind.Place, BrainstormingSourceKind.Timeline }) refs.Add(BrainstormingStorySources.Canon(pending.DocumentId, kind, null, pending.ServerDocumentId, pending.ServerVersion, dirty) with { Availability = BrainstormingSourceAvailability.NotLoaded, Freshness = BrainstormingFreshness.Cached });
        var result = new BrainstormingStorySnapshot(1, outline, pending.Title, BrainstormingStorySources.Stamp(pending.LocalRevision.ToString(), outline, new { Pages = pages, References = refs }), pages, refs, false,
            "This is checked local context, including pending writing. Server generation needs a fresh authenticated source check after synchronization; the server transport cannot yet authorize newer local text. No cloud text was substituted.");
        BrainstormingStorySources.Validate(result); return result;
    }
    public async Task<BrainstormingPreparation> PrepareAsync(LocalEditorSession editor, BrainstormingTargetQuery query, IReadOnlyList<BrainstormingContextChoice> choices,
        IReadOnlyList<Guid> supportingDocuments, int budget, BrainstormingScope? sessionScope = null, BrainstormingSession? previous = null, CancellationToken ct = default)
    {
        var generation = account.Generation; var saved = editor.Document; var pending = editor.Snapshot(); var source = Capture(saved, pending);
        var currentScope = await conversations.ScopeAsync(saved.DocumentId, sessionScope?.SessionId, ct);
        var scope = sessionScope ?? currentScope;
        if (scope != currentScope) throw new InvalidDataException("Review account/backend/document changed.");
        var refs = source.References.ToList();
        foreach (var kind in Enum.GetValues<CanonKind>()) {
            var type = Enum.Parse<BrainstormingSourceKind>(kind.ToString()); var id = BrainstormingStorySources.Identity(pending.DocumentId, type.ToString());
            if (!choices.Any(c => c.SourceId == id && c.Enabled) || !canon.CanView) continue;
            var cache = await canon.CachedAsync(pending, kind, ct);
            refs.RemoveAll(r => r.Id == id); refs.Add(BrainstormingStorySources.Canon(pending.DocumentId, type, cache?.Snapshot, pending.ServerDocumentId,
                pending.ServerVersion, editor.IsDirty || pending.SyncState != LocalSyncState.Synced || cache?.RefreshState != "Ready"));
        }
        if (supportingDocuments.Count > 10 || supportingDocuments.Distinct().Count() != supportingDocuments.Count || supportingDocuments.Contains(saved.DocumentId)) throw new InvalidDataException("Select at most ten distinct supporting documents.");
        var supports = new List<LocalDocument>();
        foreach (var id in supportingDocuments) {
            var document = await documents.LoadAsync(id, ct) ?? throw new InvalidDataException("Selected local supporting document is unavailable.");
            var captured = Capture(document, document); supports.Add(document);
            refs.Add(BrainstormingStorySources.Supporting(captured, await BrainstormingStorySources.ProjectAsync(captured, projector, ct)));
        }
        source = BrainstormingStorySources.WithReferences(source, refs);
        var projection = await BrainstormingStorySources.ProjectAsync(source, projector, ct);
        var latest = await documents.LoadAsync(saved.DocumentId, ct);
        if (generation != account.Generation || latest is null || latest.DeletedAtUtc is not null || DeviceSyncMapping.Fingerprint(latest) != DeviceSyncMapping.Fingerprint(saved)
            || DeviceSyncMapping.Fingerprint(editor.Snapshot()) != DeviceSyncMapping.Fingerprint(pending)) throw new InvalidDataException("Writing/account changed during capture. Refresh context; pending text is retained.");
        foreach (var support in supports) if (await documents.LoadAsync(support.DocumentId, ct) is not { DeletedAtUtc: null } current || DeviceSyncMapping.Fingerprint(current) != DeviceSyncMapping.Fingerprint(support)) throw new InvalidDataException("A selected supporting document changed. Refresh context.");
        ct.ThrowIfCancellationRequested(); return BrainstormingContextPolicy.Prepare(scope, source, projection, query, choices, budget, previous);
    }
}
