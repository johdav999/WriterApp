using WriterApp.Shared.Brainstorming;

namespace WriterApp.Client.Services;

public sealed class WebBrainstormingSessions(WebBrainstormingStore files, HttpClient http, AuthStateService auth) : IBrainstormingCacheStore, IBrainstormingReviewIdentity
{
    private long _accessEpoch;
    private event Action? ReviewRevoked;
    private void Revoke(){_accessEpoch++;ReviewRevoked?.Invoke();}
    public string Identity => Backend + ":" + Account+":"+_accessEpoch;
    public BrainstormingKeepHttp Keeps => new(http,RemoteLease);
    public BrainstormingPlanningHttp Planning => new(http,RemoteLease);
    public string? ActiveAccountKey => Account;
    public event Action? Changed { add {auth.Changed += value;ReviewRevoked+=value;} remove {auth.Changed -= value;ReviewRevoked-=value;} }
    private string Backend => BrainstormingValidation.Fingerprint(http.BaseAddress?.AbsoluteUri ?? throw new InvalidDataException("Missing backend."));
    private string? Account => auth.IsAuthenticated && auth.UserId is {Length:>0} user ? BrainstormingValidation.Fingerprint(user) : null;
    public BrainstormingSessionService Sessions => new(this, Remote);
    public BrainstormingDictationWorkflow DictationWorkflow=>new(this,new BrainstormingSpeechHttp(http,RemoteLease,SpeechScope));
    private async Task<BrainstormingScope> SpeechScope(BrainstormingScope local,CancellationToken ct) {var check=await Guard(local,ct);var cache=await ReadAsync(local,ct)??throw new InvalidDataException("Conversation unavailable.");if(cache.Mapping is null)cache=await SyncAsync(local,ct:ct);check();return cache.Mapping?.Server??throw new InvalidDataException("Synchronize this owned conversation first.");}
    public BrainstormingConversationHttp Conversation => new(http, RemoteLease);
    public BrainstormingConversationWorkflow ConversationWorkflow => new(this, Conversation, (scope,pull,ct)=>SyncAsync(scope,pull,ct));
    public IBrainstormingRemote Remote => new BrainstormingHttpRemote(http, RemoteLease,
        doc => new(1, Guid.NewGuid(), doc, null, BrainstormingIdentitySpace.Server, Backend, Account, null));
    public Task ClearReviewIdentityAsync(CancellationToken ct = default) => files.RememberIdentityAsync(Backend, null, ct);
    private async Task<string?> ReviewAccount(CancellationToken ct) {
        await auth.GetAsync(ct: ct);
        if (Account is { } account) { await files.RememberIdentityAsync(Backend, account, ct); return account; }
        if (auth.HasResolvedState) { await ClearReviewIdentityAsync(ct); return null; }
        return await files.IsOfflineAsync(ct) ? await files.ReviewIdentityAsync(Backend, ct) : null;
    }
    private async Task<Action> Guard(BrainstormingScope scope, CancellationToken ct) {
        var baseAddress = http.BaseAddress; var account = await ReviewAccount(ct); var resolved = auth.HasResolvedState;var epoch=_accessEpoch;
        void Check() { ct.ThrowIfCancellationRequested(); BrainstormingValidation.Validate(scope);
            if (scope.Space != BrainstormingIdentitySpace.Local || scope.BackendKey != Backend || scope.AccountKey is null || scope.AccountKey != account
                || baseAddress != http.BaseAddress || Account is { } current && current != account || resolved && Account != account
                || !resolved && auth.HasResolvedState && Account != account || epoch!=_accessEpoch) throw new OperationCanceledException("Conversation account/backend changed.", ct); }
        Check(); return Check;
    }
    private BrainstormingRemoteLease RemoteLease(BrainstormingScope scope) {
        var baseAddress = http.BaseAddress; var account = Account;var epoch=_accessEpoch;
        void Check() { if (account is null || scope.AccountKey != account || Account != account || scope.BackendKey != Backend
            || baseAddress != http.BaseAddress || epoch!=_accessEpoch || scope.Space != BrainstormingIdentitySpace.Server) throw new OperationCanceledException("Sign in to the original account/backend before exchanging sessions."); }
        Check(); return new(Check,AccessRevoked:Revoke);
    }
    public async Task<BrainstormingScope> ScopeAsync(Guid document, Guid? project, Guid? session = null, CancellationToken ct = default) {
        var account = await ReviewAccount(ct) ?? throw new InvalidOperationException("Sign in before creating/reopening this account's conversations.");
        return new(1, session ?? Guid.NewGuid(), document, project, BrainstormingIdentitySpace.Local, Backend, account, null);
    }
    public async Task<BrainstormingCache?> ReadAsync(BrainstormingScope scope, CancellationToken ct = default) { var check = await Guard(scope, ct); var value = await files.ReadAsync(scope, ct); check(); return value; }
    public async Task<BrainstormingCache> WriteAsync(BrainstormingCache value, long expectedRevision, Guid operationId, CancellationToken ct = default) { var check = await Guard(value.Session.Scope, ct); var saved = await files.WriteAsync(value, expectedRevision, operationId, ct); check(); return saved; }
    public async Task<IReadOnlyList<BrainstormingSummary>> ListAsync(BrainstormingScope scope, Guid? after = null, int limit = 25, CancellationToken ct = default) { var check = await Guard(scope, ct); var value = await files.ListAsync(scope, after, limit, ct); check(); return value; }
    public async Task<IReadOnlyList<BrainstormingRejectedWrite>> ReadRejectedAsync(BrainstormingScope scope, CancellationToken ct = default) { var check = await Guard(scope, ct); var value = await files.ReadRejectedAsync(scope, ct); check(); return value; }
    public Task<BrainstormingCache> MapAsync(BrainstormingScope scope, CancellationToken ct = default) => Sessions.MapAsync(scope, scope with { Space = BrainstormingIdentitySpace.Server }, ct);
    public async Task<BrainstormingCache> SyncAsync(BrainstormingScope scope, bool pull = false, CancellationToken ct = default) {
        var check = await Guard(scope, ct); var value = await Sessions.ReadAsync(scope, ct) ?? throw new InvalidDataException("Missing session.");
        if(value.Mapping is null)value=await MapAsync(scope,ct);
        var docs = value.Session.Contexts.SelectMany(c => c.Sources).Select(s => s.DocumentId).Concat(value.Session.EnabledSources?.Select(s => s.DocumentId) ?? []).Append(scope.DocumentId).Distinct().ToDictionary(id => id);
        var sections = value.Session.Targets.SelectMany(t => new[] { t.SectionId, t.BeforeSectionId }).Concat(value.Session.Contexts.SelectMany(c => c.Sources).Select(s => s.SectionId))
            .Concat(value.Session.Contexts.SelectMany(c => c.Sources).SelectMany(s => s.Parts ?? []).Select(p => (Guid?)p.SectionId)).OfType<Guid>().Distinct().ToDictionary(id => id);
        var pages = value.Session.Targets.Select(t => t.PageId).Concat(value.Session.Contexts.SelectMany(c => c.Sources).Select(s => s.PageId))
            .Concat(value.Session.Contexts.SelectMany(c => c.Sources).SelectMany(s => s.Parts ?? []).Select(p => (Guid?)p.PageId)).OfType<Guid>().Distinct().ToDictionary(id => id);
        var nodes = value.Session.Targets.SelectMany(t => new[] { t.ChapterNodeId, t.SceneNodeId, t.ParentNodeId, t.BeforeNodeId }).OfType<Guid>().Distinct().ToDictionary(id => id);
        foreach (var key in value.Session.Planning.Select(p => p.EntityKey).Concat(value.Session.EnabledSources?.Select(s => s.EntityKey) ?? [])) if (Guid.TryParse(key, out var id)) nodes.TryAdd(id, id);
        var map = new BrainstormingIdentityMap(docs, sections, pages, nodes, PreserveServerIds: true);
        var result=pull ? await Sessions.PullAsync(scope, map, check, ct) : await Sessions.SyncAsync(scope, map, check, ct);
        return await BrainstormingRecovery.ExchangeAsync(this,Remote,result.Session.Scope,check,ct);
    }
    public async Task DiscoverAsync(BrainstormingScope partition,CancellationToken ct=default) {
        var check=await Guard(partition,ct);Guid? cursor=null;
        do {var page=await Remote.ListAsync(partition.DocumentId,cursor,25,ct);check();
            foreach(var item in page.Items.Where(s=>s.Scope.ProjectId==partition.ProjectId)){var server=await Remote.ReadAsync(item.Scope,ct);check();if(server is null)continue;
                var existing=await ListAsync(partition,limit:50,ct:ct);var already=false;Guid? localCursor=null;
                do {foreach(var summary in existing){var cache=await ReadAsync(summary.Scope,ct);if(cache?.Mapping?.Server==server.Scope){already=true;break;}}
                    if(already || existing.Count<50)break;localCursor=existing[^1].Scope.SessionId;existing=await ListAsync(partition,localCursor,50,ct);
                }while(true);if(already)continue;
                var local=partition with {SessionId=server.Scope.SessionId};
                var map=new BrainstormingIdentityMap(new Dictionary<Guid,Guid>{{local.DocumentId,server.Scope.DocumentId}},new Dictionary<Guid,Guid>(),new Dictionary<Guid,Guid>(),new Dictionary<Guid,Guid>(),true);
                await Sessions.ImportAsync(local,server,map,check,ct);await BrainstormingRecovery.ExchangeAsync(this,Remote,local,check,ct);
            }cursor=page.NextAfter;
        }while(cursor is not null);
    }
    public Task<BrainstormingCache> ForkServerAsync(BrainstormingScope scope)=>Sessions.ForkServerAsync(scope,Guid.NewGuid(),new(new Dictionary<Guid,Guid>{{scope.DocumentId,scope.DocumentId}},new Dictionary<Guid,Guid>(),new Dictionary<Guid,Guid>(),new Dictionary<Guid,Guid>(),true));
}
