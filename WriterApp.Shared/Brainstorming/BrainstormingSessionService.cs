namespace WriterApp.Shared.Brainstorming;

/// <summary>Portable persistence workflow for presentation. Sync is explicit, read/write only; never sends AI or applies content.</summary>
public sealed class BrainstormingSessionService(IBrainstormingCacheStore store, IBrainstormingRemote remote)
{
    private static BrainstormingSession Accepted(BrainstormingSession session) => session with { Turns = session.Turns.Select(t => t.Role == BrainstormingRole.User
        && t.State == BrainstormingTurnState.Unsent ? t with { State = BrainstormingTurnState.Accepted } : t).ToArray() };
    public Task<BrainstormingCache?> ReadAsync(BrainstormingScope scope, CancellationToken ct = default) => store.ReadAsync(scope, ct);
    public async Task<BrainstormingCache> CreateAsync(BrainstormingScope scope, CancellationToken ct = default) =>
        await store.WriteAsync(new(1, 1, BrainstormingPersistence.Empty(scope), "", null, null, null, [], []), 0, Guid.NewGuid(), ct);
    public async Task<BrainstormingCache> CreateDictatedDraftAsync(BrainstormingScope scope,string correctedText,Guid dictationOperation,CancellationToken ct=default) {
        var cache=await Required(scope,ct);if(cache.Pending is not null || cache.PendingGeneration is not null || cache.Conflicts.Count>0 || cache.Session.State==BrainstormingConversationState.Deleted)throw new InvalidDataException("Settle the retained operation/conflict before placing dictated prose.");
        var journal=(cache.Dictations??[]).SingleOrDefault(j=>j.OperationId==dictationOperation && j.Result?.State==BrainstormingSpeechState.Completed)??throw new InvalidDataException("Inspect a completed dictation first.");
        var target=cache.Session.Targets.LastOrDefault()??throw new InvalidDataException("Review a prose destination first.");var context=cache.Session.Contexts.LastOrDefault()??throw new InvalidDataException("Review current story context first.");
        if(context.Policy?.Ready!=true || string.IsNullOrWhiteSpace(correctedText) || correctedText.Length>8000)throw new InvalidDataException("Review context and correct the dictated prose before placing it.");
        if(cache.Session.Drafts.Any(d=>d.Origin==BrainstormingDraftOrigin.AuthorDictated && d.DictationOperationId==dictationOperation && d.PlainText==correctedText && d.TargetId==target.Id && d.ContextId==context.Id))return cache;
        var now=DateTimeOffset.UtcNow;var draft=new BrainstormingDraft(1,Guid.NewGuid(),scope,Guid.Empty,Guid.Empty,context.Id,target.Id,null,1,true,"Author placed corrected dictation as proposed prose",correctedText,"Author supplied; unapplied until separate Keep review",[],now,BrainstormingDraftContent.Html(correctedText),Origin:BrainstormingDraftOrigin.AuthorDictated,DictationOperationId:journal.OperationId);
        return await store.WriteAsync(cache with {Session=cache.Session with {Revision=cache.Session.Revision+1,UpdatedAt=now,State=cache.Mapping is null?BrainstormingConversationState.LocalOnly:BrainstormingConversationState.PendingSave,Drafts=[..cache.Session.Drafts,draft]},DraftReview=new(1,draft.Id,true)},cache.CacheRevision,Guid.NewGuid(),ct);
    }
    public Task<BrainstormingCache> SaveAsync(BrainstormingCache value, long expectedRevision, Guid operationId, CancellationToken ct = default) =>
        store.WriteAsync(value, expectedRevision, operationId, ct);
    public async Task<BrainstormingCache> RememberPreparationAsync(BrainstormingScope scope, BrainstormingPreparation preparation, CancellationToken ct = default)
    {
        if (preparation.Version != 1 || preparation.Context is null || preparation.Resolution.Target is null) throw new InvalidDataException("Resolve the context and destination before saving preparation.");
        BrainstormingValidation.Validate(preparation.Context, scope); BrainstormingValidation.Validate(preparation.Resolution.Target, scope.DocumentId);
        var current = await Required(scope, ct);
        for(int attempt=0;;attempt++) {
        if (current.Session.State is BrainstormingConversationState.Deleted or BrainstormingConversationState.Conflict) throw new InvalidDataException("Resolve or branch this conversation first.");
        var session = current.Session with { Revision = current.Session.Revision + 1, UpdatedAt = DateTimeOffset.UtcNow,
            State = current.Mapping is null ? BrainstormingConversationState.LocalOnly : BrainstormingConversationState.PendingSave,
            Contexts = [..current.Session.Contexts, preparation.Context], Targets = [..current.Session.Targets, preparation.Resolution.Target] };
        var review = current.DraftReview;
        if (BrainstormingDrafts.Candidate(current) is { } candidate
            && current.Keeps?.Any(k => k.Review.Candidate.Id == candidate.Id && k.Receipt?.State == BrainstormingApplicationState.Committed) == true) {
            var original = current.Session.Targets.Single(t => t.Id == candidate.TargetId);
            if (BrainstormingDrafts.SameLocation(original, preparation.Resolution.Target)
                && !BrainstormingDrafts.SameDestination(original, preparation.Resolution.Target))
                review = new(1, candidate.Id, false);
        }
        // Old contexts, proposals, approvals, turns and unsent composer remain immutable and inspectable.
        try {return await store.WriteAsync(current with { Session = session, DraftReview = review }, current.CacheRevision, Guid.NewGuid(), ct);}
        catch(BrainstormingConcurrencyException) when(attempt<3) {
            var latest=await Required(scope,ct);
            // Typing is independent of context capture. Rebase only that field;
            // a changed conversation, operation or review still requires inspection.
            if(BrainstormingPersistence.Json(current with {CacheRevision=latest.CacheRevision,Composer=latest.Composer,LocalReceipts=latest.LocalReceipts})!=BrainstormingPersistence.Json(latest))throw;
            current=latest;
        }
        }
    }
    public async Task<BrainstormingCache> SaveComposerAsync(BrainstormingScope scope, string text, CancellationToken ct = default)
    {
        var current = await Required(scope, ct);
        for(int attempt=0;;attempt++) {
            if(current.Composer==text)return current;
            try {return await store.WriteAsync(current with { Composer = text }, current.CacheRevision, Guid.NewGuid(), ct);}
            catch(BrainstormingConcurrencyException) when(attempt<3) {
                var latest=await Required(scope,ct);
                if(latest.Composer!=current.Composer && latest.Composer!=text)throw;
                current=latest;
            }
        }
    }
    public async Task<BrainstormingCache> AppendUnsentAsync(BrainstormingScope scope, string text, Guid sendOperationId, CancellationToken ct = default)
    {
        var current = await Required(scope, ct);
        if (current.Session.State is BrainstormingConversationState.Deleted or BrainstormingConversationState.Conflict) throw new InvalidDataException("Resolve or branch this session first.");
        var prior = current.Session.Turns.SingleOrDefault(t => t.Role == BrainstormingRole.User && t.OperationId == sendOperationId);
        if (prior is not null) { if (prior.Text != text) throw new InvalidDataException("Send operation text changed."); return current; }
        var turn = new BrainstormingTurn(1, Guid.NewGuid(), scope, current.Session.Turns.Count + 1, current.Session.Turns.LastOrDefault()?.Id,
            sendOperationId, null, null, BrainstormingRole.User, BrainstormingTurnState.Unsent, text, DateTimeOffset.UtcNow);
        var session = current.Session with { Revision = current.Session.Revision + 1, State = current.Mapping is null ? BrainstormingConversationState.LocalOnly : BrainstormingConversationState.PendingSave,
            UpdatedAt = DateTimeOffset.UtcNow, Turns = [..current.Session.Turns, turn] };
        return await store.WriteAsync(current with { Session = session, Composer = "" }, current.CacheRevision, Guid.NewGuid(), ct);
    }
    public async Task<BrainstormingCache> MapAsync(BrainstormingScope local, BrainstormingScope server, CancellationToken ct = default)
    {
        var value = await Required(local, ct); var mapping = new BrainstormingMapping(1, local, server, 1, DateTimeOffset.UtcNow);
        BrainstormingValidation.Validate(mapping);
        if (value.Mapping is not null && value.Mapping.Server != server) throw new InvalidDataException("Existing mapping was retained. Reopen the original backend/account or explicitly copy the session.");
        return await store.WriteAsync(value with { Mapping = value.Mapping ?? mapping }, value.CacheRevision, Guid.NewGuid(), ct);
    }
    public async Task<BrainstormingCache> SyncAsync(BrainstormingScope scope, BrainstormingIdentityMap identities, Action requireCurrent, CancellationToken ct = default,
        Func<CancellationToken, Task>? requireMapping = null)
    {
        requireCurrent(); var value = await Required(scope, ct); var mapping = value.Mapping ?? throw new InvalidDataException("Explicit authenticated session mapping is required.");
        if (value.Pending is null && value.Acknowledgement is not null && (value.Session.State == BrainstormingConversationState.Saved
            || value.Session.State == BrainstormingConversationState.Deleted && value.DeletionAcknowledged))
            return value;
        if (value.Conflicts.Count != 0) throw new InvalidDataException("Both branches are retained. Resolve or fork explicitly before syncing.");
        // Persist one immutable outbox payload before HTTP. A failed/unknown acknowledgement keeps exactly this operation.
        if (value.Pending is null) {
            var expected = value.Acknowledgement?.Revision ?? 0;
            var converted = Accepted(identities.Convert(value.Session, mapping.Server)) with { Revision = expected + 1,
                State = value.Session.State == BrainstormingConversationState.Deleted ? BrainstormingConversationState.Deleted : BrainstormingConversationState.Saved };
            if (value.Session.BranchedFromSessionId is { } parentId) {
                var parent = await store.ReadAsync(scope with { SessionId = parentId }, ct);
                converted = converted with { BranchedFromSessionId = parent?.Acknowledgement is not null ? parent.Mapping?.Server.SessionId : null };
            }
            // Local request revision clocks are not server session clocks.
            converted = converted with { Requests = converted.Requests.Select(r => r with { ExpectedSessionRevision = Math.Min(r.ExpectedSessionRevision, converted.Revision) }).ToArray() };
            var write = new BrainstormingWrite(1, Guid.NewGuid(), expected, converted); BrainstormingPersistence.Validate(write);
            requireCurrent(); value = await store.WriteAsync(value with { Pending = write }, value.CacheRevision, Guid.NewGuid(), ct);
        }
        var pending = value.Pending!; requireCurrent(); if (requireMapping is not null) await requireMapping(ct);
        try {
            var receipt = await remote.InspectAsync(mapping.Server, pending.OperationId, ct);
            requireCurrent(); if (requireMapping is not null) await requireMapping(ct);
            receipt ??= await remote.WriteAsync(pending, ct); requireCurrent(); if (requireMapping is not null) await requireMapping(ct);
            BrainstormingPersistence.RequireReceipt(pending, receipt);
            // Do not overwrite edits/composer changes made while HTTP was in flight.
            var latest = await Required(scope, ct);
            if (latest.Pending?.OperationId != pending.OperationId) {
                if (latest.Acknowledgement?.OperationId == pending.OperationId) return latest;
                throw new BrainstormingConcurrencyException(latest.Session);
            }
            var currentServer = Accepted(identities.Convert(latest.Session, mapping.Server)) with { Revision = pending.Session.Revision, State = pending.Session.State,
                Requests = latest.Session.Requests.Select(r => r with { Scope = mapping.Server, ExpectedSessionRevision = Math.Min(r.ExpectedSessionRevision, pending.Session.Revision) }).ToArray() };
            currentServer = currentServer with { BranchedFromSessionId = pending.Session.BranchedFromSessionId };
            bool unchanged = BrainstormingPersistence.Json(currentServer) == BrainstormingPersistence.Json(pending.Session);
            var session = latest.Session with { State = unchanged ? pending.Session.State : BrainstormingConversationState.PendingSave,
                Turns = latest.Session.Turns.Select(t => pending.Session.Turns.Any(sent => sent.Id == t.Id && sent.OperationId == t.OperationId
                    && sent.Text == t.Text && sent.State == BrainstormingTurnState.Accepted) && t.State == BrainstormingTurnState.Unsent
                    ? t with { State = BrainstormingTurnState.Accepted } : t).ToArray() };
            requireCurrent(); return await store.WriteAsync(latest with { Session = session, Pending = null, Acknowledgement = receipt,
                DeletionAcknowledged = unchanged && pending.Session.State == BrainstormingConversationState.Deleted,
                Mapping = mapping with { Revision = mapping.Revision + 1, UpdatedAt = DateTimeOffset.UtcNow } }, latest.CacheRevision, Guid.NewGuid(), ct);
        } catch (BrainstormingConcurrencyException conflict) when (conflict.Current?.Scope.Space == BrainstormingIdentitySpace.Server) {
            var latest = await Required(scope, ct); requireCurrent(); if (requireMapping is not null) await requireMapping(ct);
            BrainstormingValidation.RequireScope(conflict.Current.Scope, mapping.Server);
            var kept = new BrainstormingConflict(pending.OperationId, latest.Session, conflict.Current, DateTimeOffset.UtcNow);
            return await store.WriteAsync(latest with { Session = latest.Session with { State = latest.Session.State == BrainstormingConversationState.Deleted
                    ? BrainstormingConversationState.Deleted : BrainstormingConversationState.Conflict },
                Conflicts = [..latest.Conflicts, kept] }, latest.CacheRevision, Guid.NewGuid(), ct);
        }
    }
    public async Task<BrainstormingCache> PullAsync(BrainstormingScope scope, BrainstormingIdentityMap identities, Action requireCurrent, CancellationToken ct = default,
        Func<CancellationToken, Task>? requireMapping = null)
    {
        requireCurrent(); var value = await Required(scope, ct); var mapping = value.Mapping ?? throw new InvalidDataException("Map this session first.");
        if (requireMapping is not null) await requireMapping(ct);
        var server = await remote.ReadAsync(mapping.Server, ct); requireCurrent(); if (requireMapping is not null) await requireMapping(ct);
        if (server is null) return value;
        BrainstormingValidation.RequireScope(server.Scope, mapping.Server);
        value = await Required(scope, ct);
        if (server.Revision == value.Acknowledgement?.Revision) return value;
        if (value.Pending is not null || value.DraftEdit is not null || value.Session.State is BrainstormingConversationState.PendingSave or BrainstormingConversationState.Conflict
            || value.Session.State == BrainstormingConversationState.Deleted && !value.DeletionAcknowledged
            || value.Acknowledgement is null && value.Session.Turns.Count != 0) {
            var conflict = new BrainstormingConflict(Guid.NewGuid(), value.Session, server, DateTimeOffset.UtcNow);
            return await store.WriteAsync(value with { Session = value.Session with { State = value.Session.State == BrainstormingConversationState.Deleted
                ? BrainstormingConversationState.Deleted : BrainstormingConversationState.Conflict }, Conflicts = [..value.Conflicts, conflict] }, value.CacheRevision, Guid.NewGuid(), ct);
        }
        // Streaming acceptance and completion each advance the server clock. A long
        // conversation must not import requests whose captured revision exceeds the
        // local snapshot clock; preserve both clocks without rewriting provenance.
        var converted = identities.Reverse().Convert(server, scope) with { Revision = Math.Max(value.Session.Revision + 1, server.Revision), BranchedFromSessionId = value.Session.BranchedFromSessionId };
        var ack = new BrainstormingReceipt(1, Guid.NewGuid(), mapping.Server, server.Revision, BrainstormingPersistence.Hash(server), DateTimeOffset.UtcNow);
        return await store.WriteAsync(value with { Session = converted, Acknowledgement = ack,
            DeletionAcknowledged = server.State == BrainstormingConversationState.Deleted }, value.CacheRevision, Guid.NewGuid(), ct);
    }
    public async Task<BrainstormingCache> ForkAsync(BrainstormingScope scope, Guid newSessionId, CancellationToken ct = default)
    {
        var original = await Required(scope, ct); var nextScope = scope with { SessionId = newSessionId };
        var session = BrainstormingPersistence.ReScope(original.Session, nextScope) with { Revision = 1, BranchedFromSessionId = scope.SessionId,
            State = BrainstormingConversationState.LocalOnly, Requests = original.Session.Requests.Select(r => r with { Scope = nextScope, ExpectedSessionRevision = 1 }).ToArray() };
        // The original branch keeps all pending operations and journals. A new branch is discussion,
        // not a replayable copy of a committed application or an active generation.
        session=session with {Approvals=[],Applications=[],Requests=session.Requests.Select(r=>r.State is BrainstormingGenerationState.Accepted or BrainstormingGenerationState.Streaming or BrainstormingGenerationState.Unknown?r with {State=BrainstormingGenerationState.Interrupted}:r).ToArray()};
        var copied=new BrainstormingCache(1,1,session,original.Composer,null,null,null,[],[],DraftReview:original.DraftReview,DraftEdit:original.DraftEdit);
        return await store.WriteAsync(copied, 0, Guid.NewGuid(), ct);
    }
    public async Task<BrainstormingCache> ForkServerAsync(BrainstormingScope scope,Guid newSessionId,BrainstormingIdentityMap identities,CancellationToken ct=default) {
        var original=await Required(scope,ct);var server=original.Conflicts.LastOrDefault()?.Server??throw new InvalidDataException("No retained server branch.");
        var destination=scope with {SessionId=newSessionId};var session=identities.Reverse().Convert(server,destination) with {Revision=1,State=BrainstormingConversationState.LocalOnly,BranchedFromSessionId=scope.SessionId,Approvals=[],Applications=[],
            Requests=server.Requests.Select(r=>r with {Scope=destination,ExpectedSessionRevision=1,State=r.State is BrainstormingGenerationState.Accepted or BrainstormingGenerationState.Streaming or BrainstormingGenerationState.Unknown?BrainstormingGenerationState.Interrupted:r.State}).ToArray()};
        return await store.WriteAsync(new(1,1,session,"",null,null,null,[],[]),0,Guid.NewGuid(),ct);
    }
    public async Task<BrainstormingCache> ImportAsync(BrainstormingScope local,BrainstormingSession server,BrainstormingIdentityMap identities,Action check,CancellationToken ct=default) {
        check();BrainstormingValidation.Validate(server);
        var existing=await store.ReadAsync(local,ct);if(existing is not null)return existing;
        var mapping=new BrainstormingMapping(1,local,server.Scope,1,DateTimeOffset.UtcNow);BrainstormingValidation.Validate(mapping);
        var converted=identities.Reverse().Convert(server,local) with {BranchedFromSessionId=null};
        var ack=new BrainstormingReceipt(1,Guid.NewGuid(),server.Scope,server.Revision,BrainstormingPersistence.Hash(server),DateTimeOffset.UtcNow);
        check();return await store.WriteAsync(new(1,1,converted,"",mapping,null,ack,[],[],server.State==BrainstormingConversationState.Deleted),0,Guid.NewGuid(),ct);
    }
    public async Task<BrainstormingCache> DeleteAsync(BrainstormingScope scope, CancellationToken ct = default) {
        var current = await Required(scope, ct);
        if (current.Session.State == BrainstormingConversationState.Deleted) return current;
        if (current.Pending is not null || current.Session.State == BrainstormingConversationState.Conflict) throw new InvalidDataException("Settle the pending operation/conflict before deleting. Its data was retained.");
        return await store.WriteAsync(current with { Session = current.Session with { State = BrainstormingConversationState.Deleted,
            Revision = current.Session.Revision + 1, UpdatedAt = DateTimeOffset.UtcNow } }, current.CacheRevision, Guid.NewGuid(), ct);
    }
    public async Task<BrainstormingCache> ReviewDraftAsync(BrainstormingScope scope, Guid candidateId, bool useForNextRequest,
        BrainstormingDraftSelection? selection = null, CancellationToken ct = default) {
        var cache=await Required(scope,ct);
        if(cache.DraftEdit is not null || cache.PendingGeneration is not null)throw new InvalidDataException("Checkpoint the retained proposal edit and settle generation before changing versions.");
        var next=cache with{DraftReview=new(1,candidateId,useForNextRequest,selection)};
        BrainstormingDrafts.ValidateReview(next);
        return await store.WriteAsync(next,cache.CacheRevision,Guid.NewGuid(),ct);
    }
    public Task<BrainstormingCache> CreateAlternativeAsync(BrainstormingScope scope,string name,CancellationToken ct=default) => CopyWorkingVersionAsync(scope,null,name,ct);
    public Task<BrainstormingCache> RestoreDraftAsync(BrainstormingScope scope,Guid version,CancellationToken ct=default) => CopyWorkingVersionAsync(scope,version,null,ct);
    private async Task<BrainstormingCache> CopyWorkingVersionAsync(BrainstormingScope scope,Guid? restore,string? name,CancellationToken ct) {
        var cache=await Required(scope,ct);
        if(cache.PendingGeneration is not null || cache.Pending is not null || cache.DraftEdit is not null || cache.Conflicts.Count!=0 || cache.Session.State==BrainstormingConversationState.Deleted)
            throw new InvalidDataException("Finish saving this draft and settle pending operations before changing alternatives or restoring a version.");
        var source=restore is { } id?cache.Session.Drafts.Single(d=>d.Id==id):BrainstormingDrafts.Candidate(cache)??throw new InvalidDataException("Create a working draft first.");
        name=name?.Trim();
        if(restore is null && (string.IsNullOrWhiteSpace(name) || name.Length>60 || name.Any(char.IsControl)))throw new InvalidDataException("Name the alternative using 1–60 characters.");
        var now=DateTimeOffset.UtcNow;
        var draft=source with {Id=Guid.NewGuid(),ParentDraftId=source.Id,Revision=source.Revision+1,ManuallyEdited=true,CreatedAt=now,
            BranchId=restore is null?Guid.NewGuid():source.BranchId,BranchName=restore is null?name:source.BranchName,
            Instruction=restore is null?$"Alternative: {name}":"Restored an earlier saved version"};
        var session=cache.Session with {Revision=cache.Session.Revision+1,UpdatedAt=now,State=cache.Mapping is null?BrainstormingConversationState.LocalOnly:BrainstormingConversationState.PendingSave,Drafts=[..cache.Session.Drafts,draft]};
        return await store.WriteAsync(cache with {Session=session,DraftReview=new(1,draft.Id,true)},cache.CacheRevision,Guid.NewGuid(),ct);
    }
    public async Task<BrainstormingCache> SaveDraftEditAsync(BrainstormingScope scope,Guid candidateId,string baseFingerprint,string html,string plainText,CancellationToken ct=default) {
        var cache=await Required(scope,ct);
        if(cache.PendingGeneration is not null || cache.Pending is not null || cache.Conflicts.Count!=0 || cache.Session.State==BrainstormingConversationState.Deleted)
            throw new InvalidDataException("Settle the pending operation or conflict before editing this proposal. Writing is retained.");
        if(BrainstormingDrafts.Candidate(cache)?.Id!=candidateId)throw new InvalidDataException("The reviewed version changed; the edit was not adopted into it.");
        var next=cache with {DraftEdit=new(1,candidateId,baseFingerprint,html,plainText,DateTimeOffset.UtcNow),DraftReview=new(1,candidateId,true)};
        BrainstormingDrafts.ValidateReview(next);
        return await store.WriteAsync(next,cache.CacheRevision,Guid.NewGuid(),ct);
    }
    public async Task<BrainstormingCache> CheckpointDraftAsync(BrainstormingScope scope,CancellationToken ct=default) {
        var cache=await Required(scope,ct);if(cache.DraftEdit is not { } edit)return cache;
        if(cache.PendingGeneration is not null || cache.Pending is not null || cache.Conflicts.Count!=0 || cache.Session.State==BrainstormingConversationState.Deleted)
            throw new InvalidDataException("Settle the retained operation before checkpointing this proposal.");
        BrainstormingDrafts.ValidateReview(cache);
        var parent=cache.Session.Drafts.Single(d=>d.Id==edit.CandidateId);
        if(parent.EditorHtml==edit.Html && parent.PlainText==edit.PlainText)return await store.WriteAsync(cache with{DraftEdit=null},cache.CacheRevision,Guid.NewGuid(),ct);
        var now=DateTimeOffset.UtcNow;
        var draft=parent with {Id=Guid.NewGuid(),ParentDraftId=parent.Id,Revision=parent.Revision+1,ManuallyEdited=true,
            Instruction="Author's manual proposal edit",PlainText=edit.PlainText,EditorHtml=edit.Html,CreatedAt=now};
        var session=cache.Session with{Revision=cache.Session.Revision+1,UpdatedAt=now,
            State=cache.Mapping is null?BrainstormingConversationState.LocalOnly:BrainstormingConversationState.PendingSave,Drafts=[..cache.Session.Drafts,draft]};
        return await store.WriteAsync(cache with{Session=session,DraftEdit=null,DraftReview=new(1,draft.Id,true)},cache.CacheRevision,Guid.NewGuid(),ct);
    }
    private async Task<BrainstormingCache> Required(BrainstormingScope scope, CancellationToken ct) => await store.ReadAsync(scope, ct)
        ?? throw new InvalidDataException("Conversation not found. Existing sessions were preserved.");
}
