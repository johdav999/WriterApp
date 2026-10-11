using System.Net.Http.Json;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Device.Shared.Services;

/// <summary>Account/backend guards around the portable workflow and explicit device mapping.</summary>
public sealed class DeviceBrainstormingSessions(LocalBrainstormingStore files, DeviceAccountService account, DeviceHostOptions host,
    DeviceConnectivity connectivity, LocalDocumentRepository documents, HttpClient http) : IBrainstormingCacheStore, IBrainstormingReviewIdentity
{
    private long _accessEpoch;
    private Action? _reviewChanged;
    private string? _reviewIdentity;
    private string? _verifiedNative, _verifiedAccount;
    private long _verifiedGeneration=-1;
    private bool _verifiedOnline;
    private readonly SemaphoreSlim _identityGate=new(1,1);
    private string? NativeAccount => string.IsNullOrWhiteSpace(account.AccountId) || !account.IsSignedIn ? null : BrainstormingValidation.Fingerprint(account.AccountId);
    private async Task ResolveAccountAsync(CancellationToken ct) {
        await _identityGate.WaitAsync(ct);
        try {
            var native=NativeAccount;var generation=account.Generation;var online=connectivity.IsOnline;
            if(native is null){_verifiedNative=null;_verifiedAccount=null;_verifiedGeneration=generation;return;}
            if(_verifiedNative==native && _verifiedGeneration==generation && (_verifiedOnline || !online))return;
            string? canonical;
            if(!online) canonical=await files.ReadBackendAccountAsync(Backend,native,ct)??native;
            else {
                using var request=new HttpRequestMessage(HttpMethod.Get,"api/auth/me");request.Options.Set(DeviceAuthenticatedHandler.ExpectedAccountGeneration,generation);
                using var response=await http.SendAsync(request,ct);response.EnsureSuccessStatusCode();
                var profile=await response.Content.ReadFromJsonAsync<WriterApp.Application.Security.AuthMeDto>(cancellationToken:ct);
                if(profile is not {IsAuthenticated:true,UserId:{Length:>0} user})throw new InvalidDataException("The backend account could not be verified. Local conversation is retained.");
                canonical=BrainstormingValidation.Fingerprint(user);
            }
            if(generation!=account.Generation || native!=NativeAccount)throw new OperationCanceledException("The account changed during backend verification.");
            if(canonical is null)throw new InvalidOperationException("Connect once to verify the backend account before reopening signed-in conversations. Local writing is retained.");
            if(online)await files.RememberBackendAccountAsync(Backend,native,canonical,ct);
            if(generation!=account.Generation || native!=NativeAccount)throw new OperationCanceledException("The account changed during backend verification.");
            _verifiedNative=native;_verifiedAccount=canonical;_verifiedGeneration=generation;_verifiedOnline=online;
        } finally {_identityGate.Release();}
    }
    private void NotifyReviewChanged() {
        var current=Identity;
        // Account.Changed also reports successful same-account token acquisition
        // and display/status updates. These do not invalidate a checked review.
        if(current==_reviewIdentity)return;
        _reviewIdentity=current;
        _reviewChanged?.Invoke();
    }
    private void Revoke(){Interlocked.Increment(ref _accessEpoch);NotifyReviewChanged();}
    public string Identity => Backend + ":" + NativeAccount + ":" + account.Generation+":"+_accessEpoch;
    public string SynchronizationDestination => host.ApiBaseAddress.GetLeftPart(UriPartial.Path);
    public Task<Action> RequireCurrentAsync(BrainstormingScope scope,CancellationToken ct=default)=>Guard(scope,ct);
    public async Task RequireApplicationSourceAsync(BrainstormingScope scope,BrainstormingTarget target,LocalDocument local,CancellationToken ct=default)
    {
        var check=await Guard(scope,ct);var captured=DeviceBrainstormingContext.Capture(local,local);
        if(captured.Stamp==target.Source){check();return;}
        var cache=await Sessions.ReadAsync(scope,ct)??throw new InvalidDataException("Conversation unavailable.");
        var server=cache.Mapping?.Server??throw new InvalidDataException("Review the synchronized original source before keeping this generated candidate.");
        var lease=RemoteLease(server);using var request=new HttpRequestMessage(HttpMethod.Get,$"api/brainstorming/sources/{server.DocumentId}?projectId={server.ProjectId}");lease.Configure?.Invoke(request);
        using var response=await http.SendAsync(request,ct);lease.RequireCurrent();check();
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Original checked server source unavailable. The proposal remains cached; application is not queued automatically.");
        var source=await WriterApp.Shared.WebAiSources.Read<BrainstormingStorySnapshot>(response.Content,ct);BrainstormingStorySources.Validate(source);
        if(source.Stamp!=target.Source || local.ServerVersion!=source.Outline.DocumentVersion)throw new InvalidOperationException("Original source changed. Synchronize and explicitly review a fresh proposal.");
        var ids=await Identities(scope,ct);
        var outline=WriterApp.Shared.WritingOutline.Create(server.DocumentId,server.ProjectId,source.Outline.DocumentVersion,
            local.Sections.Select(s=>new WriterApp.Shared.WritingOutlineSection(ids.Sections[s.SectionId],s.OrderIndex,s.Title)),
            (local.Project?.Nodes??[]).Where(n=>n.DeletionId is null).Select(n=>new WriterApp.Shared.WritingOutlineNode(ids.Nodes[n.NodeId],n.ParentId is { } p?ids.Nodes[p]:null,n.OrderIndex,n.NodeType,n.Title,n.SectionId is { } section?ids.Sections[section]:null)));
        if(outline.Fingerprint!=source.Outline.Fingerprint || source.Pages.Count!=local.Sections.Sum(s=>s.Pages.Count)
            || local.Sections.Any(s=>s.Pages.Any(p=>p.ContentFormat!=LocalContentFormat.Html || !source.Pages.Any(remote=>remote.Id==ids.Pages[p.PageId] && remote.SectionId==ids.Sections[s.SectionId] && remote.Content==p.Content && remote.Order==p.OrderIndex && remote.Title==p.Title))))
            throw new InvalidOperationException("Local and checked server writing differ. No older cloud writing was substituted.");
        check();
    }
    public string? ActiveAccountKey => Account;
    public event Action? Changed {
        add {if(_reviewChanged is null){_reviewIdentity=Identity;account.Changed+=NotifyReviewChanged;}_reviewChanged+=value;}
        remove {_reviewChanged-=value;if(_reviewChanged is null){account.Changed-=NotifyReviewChanged;_reviewIdentity=null;}}
    }
    public BrainstormingSessionService Sessions => new(this, Remote);
    public BrainstormingDictationWorkflow DictationWorkflow=>new(this,new BrainstormingSpeechHttp(http,RemoteLease,SpeechScope));
    private async Task<BrainstormingScope> SpeechScope(BrainstormingScope local,CancellationToken ct) {var check=await Guard(local,ct);var cache=await ReadAsync(local,ct)??throw new InvalidDataException("Conversation unavailable.");if(cache.Mapping is null)cache=await SyncAsync(local,ct:ct);check();var server=cache.Mapping?.Server??throw new InvalidDataException("Sign in and synchronize the manuscript/session before dictation.");RemoteLease(server).RequireCurrent();return server;}
    public BrainstormingConversationHttp Conversation => new(http, RemoteLease);
    public BrainstormingConversationWorkflow ConversationWorkflow => new(this, Conversation, (scope,pull,ct)=>SyncAsync(scope,pull,ct));
    public IBrainstormingRemote Remote => new BrainstormingHttpRemote(http, RemoteLease,
        doc => new(1, Guid.NewGuid(), doc, null, BrainstormingIdentitySpace.Server, Backend, Account, null));
    private string Backend => BrainstormingValidation.Fingerprint(host.ApiBaseAddress.AbsoluteUri);
    private string? Account => _verifiedNative==NativeAccount && _verifiedGeneration==account.Generation ? _verifiedAccount : null;
    private async Task<Action> Guard(BrainstormingScope scope, CancellationToken ct)
    {
        await ResolveAccountAsync(ct);
        var generation = account.Generation; var owner = await files.LocalOwnerAsync(ct);var epoch=_accessEpoch;
        void Check() {
            ct.ThrowIfCancellationRequested(); BrainstormingValidation.Validate(scope);
            if (account.Generation != generation || epoch!=_accessEpoch || scope.BackendKey != Backend || scope.AccountKey != Account
                || scope.AccountKey is null && scope.LocalOwnerId != owner || scope.Space != BrainstormingIdentitySpace.Local)
                throw new OperationCanceledException("Conversation account/backend/local owner changed.", ct);
        }
        Check(); return Check;
    }
    private BrainstormingRemoteLease RemoteLease(BrainstormingScope scope)
    {
        var generation = account.Generation; var baseAddress = http.BaseAddress;var epoch=_accessEpoch;
        void Check() { if (!connectivity.IsOnline) throw new HttpRequestException("Offline: conversation remains cached; reconnect and sync explicitly.");
            if(!_verifiedOnline)throw new OperationCanceledException("Verify the backend account before exchanging this conversation. Local writing and messages are retained.");
            if (scope.BackendKey != Backend || scope.AccountKey is null || scope.AccountKey != Account || generation != account.Generation || epoch!=_accessEpoch
                || baseAddress != http.BaseAddress || baseAddress != host.ApiBaseAddress || scope.Space != BrainstormingIdentitySpace.Server)
                throw new OperationCanceledException("Conversation identity changed. Sign in to the original account/backend."); }
        Check(); return new(Check, request => request.Options.Set(DeviceAuthenticatedHandler.ExpectedAccountGeneration, generation),Revoke);
    }
    public async Task<BrainstormingScope> ScopeAsync(Guid documentId, Guid? sessionId = null, CancellationToken ct = default)
    {
        await ResolveAccountAsync(ct);
        var document = await documents.LoadAsync(documentId, ct) ?? throw new InvalidDataException("Local manuscript unavailable.");
        var scope=new BrainstormingScope(1, sessionId ?? Guid.NewGuid(), documentId, document.Project?.ProjectId, BrainstormingIdentitySpace.Local,
            Backend, Account, Account is null ? await files.LocalOwnerAsync(ct) : null);
        if(Account is not null && NativeAccount is { } native && native!=Account) {
            var check=await Guard(scope,ct);
            await files.CopyNativeDiscussionAsync(scope,native,check,ct);check();
        }
        return scope;
    }
    public async Task<BrainstormingCache?> ReadAsync(BrainstormingScope scope, CancellationToken ct = default) {
        var check = await Guard(scope, ct); var value = await files.ReadAsync(scope, ct); check(); return value;
    }
    public async Task<BrainstormingCache> WriteAsync(BrainstormingCache value, long expectedRevision, Guid operationId, CancellationToken ct = default) {
        var check = await Guard(value.Session.Scope, ct); check(); var saved = await files.WriteAsync(value, expectedRevision, operationId, ct); check(); return saved;
    }
    public async Task<IReadOnlyList<BrainstormingSummary>> ListAsync(BrainstormingScope scope, Guid? after = null, int limit = 25, CancellationToken ct = default) {
        var check = await Guard(scope, ct); var list = await files.ListAsync(scope, after, limit, ct); check(); return list;
    }
    public async Task<IReadOnlyList<BrainstormingRejectedWrite>> ReadRejectedAsync(BrainstormingScope scope, CancellationToken ct = default) {
        var check = await Guard(scope, ct); var result = await files.ReadRejectedAsync(scope, ct); check(); return result;
    }
    public async Task<BrainstormingCache> MapAsync(BrainstormingScope scope, Guid serverSessionId, CancellationToken ct = default)
    {
        var check = await Guard(scope, ct); var doc = await documents.LoadAsync(scope.DocumentId, ct) ?? throw new InvalidDataException("Manuscript unavailable."); check();
        if (scope.AccountKey is null || doc.ServerDocumentId is null) throw new InvalidDataException("Sign in and synchronize the manuscript before mapping an authenticated session. Accountless sessions require explicit transfer.");
        var server = scope with { SessionId = serverSessionId, DocumentId = doc.ServerDocumentId.Value, ProjectId = doc.Project is null ? null : doc.Project.ServerProjectId ?? doc.ServerProjectId,
            Space = BrainstormingIdentitySpace.Server };
        return await Sessions.MapAsync(scope, server, ct);
    }
    public async Task<BrainstormingIdentityMap> Identities(BrainstormingScope scope, CancellationToken ct = default)
    {
        var cache = await Sessions.ReadAsync(scope, ct) ?? throw new InvalidDataException("Conversation unavailable.");
        var ids = cache.Session.Contexts.SelectMany(c => c.Sources).Select(s => s.DocumentId).Concat(cache.Session.EnabledSources?.Select(s => s.DocumentId) ?? []).Append(scope.DocumentId).Distinct();
        var docs = new Dictionary<Guid, Guid>(); var sections = new Dictionary<Guid, Guid>(); var pages = new Dictionary<Guid, Guid>(); var nodes = new Dictionary<Guid, Guid>();
        foreach (var id in ids) {
            var doc = await documents.LoadAsync(id, ct) ?? throw new InvalidDataException("Selected local document unavailable.");
            if (doc.ServerDocumentId is not { } server || doc.DeletedAtUtc is not null) throw new InvalidDataException("Map/synchronize every selected source document before session exchange.");
            docs.Add(id, server);
            foreach (var s in doc.Sections.Concat(doc.DeletedSections.Select(x => x.Section))) {
                if (s.ServerSectionId is { } section) sections[s.SectionId] = section;
                foreach (var p in s.Pages) if (p.ServerPageId is { } page) pages[p.PageId] = page;
            }
            foreach (var p in doc.DeletedPages) if (p.Page.ServerPageId is { } page) pages[p.Page.PageId] = page;
            foreach (var n in doc.Project?.Nodes ?? []) if (n.ServerNodeId is { } node) nodes[n.NodeId] = node;
        }
        if (cache.Mapping?.Server.DocumentId != docs[scope.DocumentId]) throw new InvalidDataException("Document mapping changed. Old session/outbox retained.");
        return new(docs, sections, pages, nodes);
    }
    public async Task<BrainstormingCache> SyncAsync(BrainstormingScope scope, bool pull = false, CancellationToken ct = default) {
        var check = await Guard(scope, ct);var current=await Sessions.ReadAsync(scope,ct)??throw new InvalidDataException("Conversation unavailable.");if(current.Mapping is null)await MapAsync(scope,Guid.NewGuid(),ct);
        var ids = await Identities(scope, ct); check();
        async Task CheckMapping(CancellationToken token) {
            var current = await Identities(scope, token); check();
            static bool Same(IReadOnlyDictionary<Guid, Guid> captured, IReadOnlyDictionary<Guid, Guid> latest) => captured.All(p => latest.TryGetValue(p.Key, out var v) && v == p.Value);
            if (!Same(ids.Documents, current.Documents) || !Same(ids.Sections, current.Sections) || !Same(ids.Pages, current.Pages) || !Same(ids.Nodes, current.Nodes))
                throw new InvalidDataException("Source mapping changed during exchange. Original session and pending operation retained.");
        }
        var result=pull ? await Sessions.PullAsync(scope, ids, check, ct, CheckMapping) : await Sessions.SyncAsync(scope, ids, check, ct, CheckMapping);
        await CheckMapping(ct);return await BrainstormingRecovery.ExchangeAsync(this,Remote,result.Session.Scope,check,ct);
    }
    public async Task DiscoverAsync(BrainstormingScope partition,CancellationToken ct=default) {
        var check=await Guard(partition,ct);var doc=await documents.LoadAsync(partition.DocumentId,ct)??throw new InvalidDataException("Manuscript unavailable.");
        if(doc.ServerDocumentId is not { } document || partition.AccountKey is null)throw new InvalidDataException("Sign in and synchronize this manuscript first.");
        Guid? cursor=null;do {var page=await Remote.ListAsync(document,cursor,25,ct);check();
            foreach(var summary in page.Items.Where(s=>s.Scope.ProjectId==(doc.Project is null?null:doc.Project.ServerProjectId??doc.ServerProjectId))){var server=await Remote.ReadAsync(summary.Scope,ct);check();if(server is null)continue;
                var existing=await ListAsync(partition,limit:50,ct:ct);var already=false;
                do {foreach(var s in existing){var cached=await ReadAsync(s.Scope,ct);if(cached?.Mapping?.Server==server.Scope){already=true;break;}}
                    if(already || existing.Count<50)break;existing=await ListAsync(partition,existing[^1].Scope.SessionId,50,ct);
                }while(true);if(already)continue;
                var all=(await documents.ListAsync(LocalDocumentScope.All,ct)).Documents;var docs=new Dictionary<Guid,Guid>();var sections=new Dictionary<Guid,Guid>();var pages=new Dictionary<Guid,Guid>();var nodes=new Dictionary<Guid,Guid>();
                foreach(var id in server.Contexts.SelectMany(c=>c.Sources).Select(s=>s.DocumentId).Concat(server.EnabledSources?.Select(s=>s.DocumentId)??[]).Append(document).Distinct()) {
                    var local=all.SingleOrDefault(d=>d.ServerDocumentId==id)??throw new InvalidDataException("Synchronize selected supporting documents before importing this conversation.");docs[local.DocumentId]=id;
                    foreach(var section in local.Sections.Concat(local.DeletedSections.Select(s=>s.Section))){if(section.ServerSectionId is { } s)sections[section.SectionId]=s;foreach(var p in section.Pages)if(p.ServerPageId is { } pId)pages[p.PageId]=pId;}
                    foreach(var p in local.DeletedPages)if(p.Page.ServerPageId is { } idPage)pages[p.Page.PageId]=idPage;
                    foreach(var n in local.Project?.Nodes??[])if(n.ServerNodeId is { } node)nodes[n.NodeId]=node;
                }
                var scope=partition with {SessionId=server.Scope.SessionId};await Sessions.ImportAsync(scope,server,new(docs,sections,pages,nodes),check,ct);await BrainstormingRecovery.ExchangeAsync(this,Remote,scope,check,ct);
            }cursor=page.NextAfter;
        }while(cursor is not null);
    }
    public async Task<BrainstormingCache> ForkServerAsync(BrainstormingScope scope) {
        var check=await Guard(scope,default);var all=(await documents.ListAsync(LocalDocumentScope.All)).Documents;
        var docs=new Dictionary<Guid,Guid>();var sections=new Dictionary<Guid,Guid>();var pages=new Dictionary<Guid,Guid>();var nodes=new Dictionary<Guid,Guid>();
        foreach(var d in all.Where(d=>d.ServerDocumentId is not null)) {docs[d.DocumentId]=d.ServerDocumentId!.Value;
            foreach(var s in d.Sections.Concat(d.DeletedSections.Select(s=>s.Section))) {if(s.ServerSectionId is { } section)sections[s.SectionId]=section;foreach(var p in s.Pages)if(p.ServerPageId is { } page)pages[p.PageId]=page;}
            foreach(var p in d.DeletedPages)if(p.Page.ServerPageId is { } page)pages[p.Page.PageId]=page;
            foreach(var n in d.Project?.Nodes??[])if(n.ServerNodeId is { } node)nodes[n.NodeId]=node;
        }check();return await Sessions.ForkServerAsync(scope,Guid.NewGuid(),new(docs,sections,pages,nodes));
    }
}
