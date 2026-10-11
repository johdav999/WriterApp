using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.Application.AI;
using WriterApp.Application.Continuity;
using WriterApp.Application.Subscriptions;
using WriterApp.Application.Usage;
using WriterApp.Data;
using WriterApp.Data.Usage;
using WriterApp.Shared.Brainstorming;

// Uses only the caller's fresh owned local SQL database and fictional writing.
// No configured provider, network request, live database or real account is used.
internal static class ConversationAcceptance
{
    private static void Require(bool value,string message) {if(!value)throw new InvalidDataException(message);}
    internal sealed class CommitFault(int commit,bool after):DbTransactionInterceptor
    {
        public bool Armed;public int Seen,Failures;
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,TransactionEventData data,InterceptionResult result,CancellationToken ct=default) {
            if(Armed && !after && ++Seen==commit) {Failures++;throw new TimeoutException("Synthetic pre-commit transient failure.");}
            return ValueTask.FromResult(result);
        }
        public override Task TransactionCommittedAsync(DbTransaction transaction,TransactionEndEventData data,CancellationToken ct=default) {
            if(Armed && after && ++Seen==commit) {Failures++;throw new TimeoutException("Synthetic lost commit acknowledgment.");}
            return Task.CompletedTask;
        }
    }
    public static async Task Run(string connection,List<string> checks) {
        foreach(var mode in new[]{"normal","accept-before-commit","accept-after-commit","terminal-after-commit"}) {
            var fault=new CommitFault(mode=="terminal-after-commit"?2:1,mode!="accept-before-commit");
            var options=new DbContextOptionsBuilder<SqlServerMigrationsDbContext>().UseSqlServer(connection,s=>s.EnableRetryOnFailure()).AddInterceptors(fault).Options;
            var document=Guid.NewGuid();var section=Guid.NewGuid();var project=Guid.NewGuid();var scope=new BrainstormingScope(1,Guid.NewGuid(),document,null,BrainstormingIdentitySpace.Server,BrainstormingValidation.Fingerprint("https://test.invalid/"),BrainstormingValidation.Fingerprint("author"),null);
            const string html="<p><strong>Fictional polar expedition</strong> — Åsa 日本語 🧭.</p>";
            var provider=new Provider();var stops=new BrainstormingGenerationStops();BrainstormingSend send;
            await using(var db=new SqlServerMigrationsDbContext(options)) {
                db.Projects.Add(new(){Id=project,OwnerUserId="author",Title="Owned disposable project"});
                db.Documents.Add(new(){Id=document,ProjectId=project,OwnerUserId="author",Title="Disposable conversation"});
                db.Sections.Add(new(){Id=section,DocumentId=document,Title="Opening"});
                db.Pages.Add(new(){Id=Guid.NewGuid(),DocumentId=document,SectionId=section,Content=html});
                db.DocumentSyncRecords.Add(new(){DocumentId=document,OwnerUserId="author",Version="v1"});await db.SaveChangesAsync();
                var sources=new BrainstormingGenerationContext(db,provider,provider,"author");
                var prepared=await sources.PrepareAsync(new(1,scope,new(SectionId:section),[],[],60_000),default);
                var context=prepared.Preparation.Context!;var target=prepared.Preparation.Resolution.Target!;
                var session=BrainstormingPersistence.Empty(scope) with{State=BrainstormingConversationState.Saved,Contexts=[context],Targets=[target]};
                await new BrainstormingSessions(db,"author",scope.BackendKey).WriteAsync(new(1,Guid.NewGuid(),0,session),default);
                send=new(1,scope,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,context.Id,target.Id,null,"Explore the fictional expedition without changing writing.");
                fault.Armed=mode!="normal";
                var service=new BrainstormingConversationService(db,provider,provider,provider,provider,Microsoft.Extensions.Options.Options.Create(new WriterAiOptions{Enabled=true,Streaming=new(){Enabled=true}}),stops,sources,"author",scope.BackendKey);
                var events=new List<BrainstormingConversationEvent>();await foreach(var item in service.SendAsync(send,default))events.Add(item);
                Require(events[0].Kind==BrainstormingEventKind.Accepted,"SQL conversation acceptance missing.");
                if(mode=="accept-after-commit") {
                    Require(provider.Calls==0 && provider.Charges==0,"Uncertain accepted commit dispatched or charged a provider.");
                    Require((await service.InspectAsync(scope,send.OperationId,default))!.State==BrainstormingGenerationState.Unknown,"Lost acceptance acknowledgment was not inspectable as unknown.");
                } else {
                    Require(events[^1].Kind==BrainstormingEventKind.Completed,"SQL conversation terminal completion missing.");
                    Require(provider.Calls==1 && provider.Charges==1 && provider.Records==1,"SQL retries repeated provider dispatch or usage settlement.");
                }
                Require(mode=="normal" || fault.Failures==1,"Synthetic commit fault was not exercised.");
            }
            await using(var db=new SqlServerMigrationsDbContext(options)) {
                var sources=new BrainstormingGenerationContext(db,provider,provider,"author");
                var service=new BrainstormingConversationService(db,provider,provider,provider,provider,Microsoft.Extensions.Options.Options.Create(new WriterAiOptions{Enabled=true}),new(),sources,"author",scope.BackendKey);
                var retained=await service.InspectAsync(scope,send.OperationId,default);
                Require(retained is not null,"Generation ledger lost after restart.");
                var originalCalls=provider.Calls;var originalCharges=provider.Charges;
                await foreach(var _ in service.SendAsync(send,default)){}
                Require(provider.Calls==originalCalls && provider.Charges==originalCharges,"Duplicate immutable operation dispatched or charged again.");
                var session=(await new BrainstormingSessions(db,"author",scope.BackendKey).ReadAsync(document,scope.SessionId,default))!;
                Require(session.Requests.Count==1 && session.Turns.Count==(mode=="accept-after-commit"?1:2),"SQL retry duplicated request or terminal assistant turn.");
                Require(await db.BrainstormingGenerations.CountAsync(g=>g.SessionId==scope.SessionId)==1,"Duplicate generation ledger.");
                Require((await db.Pages.AsNoTracking().SingleAsync(p=>p.DocumentId==document)).Content==html,"Conversation changed manuscript writing.");
            }
            checks.Add("SQL retrying conversation "+mode+": durable acceptance/inspection/replay, no duplicate dispatch/charge/turn, writing retained");
        }
    }
    private sealed class Provider:IAiStreamingProvider,IAiBillingProvider,IAiRouter,IAiUsagePolicy,IAiQuotaService,IUsageMeter,IBibleStore,IEntitlementService
    {
        public int Calls,Charges,Records;
        private readonly string wire=BrainstormingPersistence.Json(new BrainstormingAnswer("Explore a storm, a discovery, or a difficult choice.",1,BrainstormingIntent.Explore,null,[],[]));
        public string ProviderId=>"local-sql-conversation-test";public AiProviderCapabilities Capabilities=>new(true,false);public AiStreamingCapabilities StreamingCapabilities=>new(true,false);public bool RequiresEntitlement=>true;public bool IsBillable=>true;
        public AiProviderSelection Route(AiRequest request)=>new(this,ProviderId,false,"test");
        public Task<AiUsageDecision> EvaluateAsync(IAiProvider provider,string action)=>Task.FromResult(new AiUsageDecision(true,"author",null,null));
        public Task<AiResult> ExecuteAsync(AiRequest request,CancellationToken ct)=>throw new NotSupportedException("Only synthetic streaming is enabled.");
        public async IAsyncEnumerable<AiStreamEvent> StreamAsync(AiRequest request,[EnumeratorCancellation]CancellationToken ct) {Calls++;yield return new AiStreamEvent.TextDelta(wire);await Task.Yield();yield return new AiStreamEvent.Completed(new(10,20,TimeSpan.Zero),"test");}
        public Task<AiQuotaDecision> EnsureAiAllowedAsync(string user,int estimate,CancellationToken ct)=>Task.FromResult(new AiQuotaDecision(true,null,null,new("Pro",1_000_000,0,DateTimeOffset.UtcNow),null));
        public Task<AiQuotaChargeResult> ChargeActualUsageAsync(string user,AiRequest request,AiResult result,CancellationToken ct){Charges++;return Task.FromResult(new AiQuotaChargeResult(true,30,new("Pro",1_000_000,30,DateTimeOffset.UtcNow),null,null,null));}
        public Task RecordAsync(UsageEvent item){Records++;return Task.CompletedTask;}
        public Task<UsageSnapshot> GetCurrentPeriodAsync(string user,string kind)=>throw new NotSupportedException();public Task<UsageSnapshot> GetRangeAsync(string user,string kind,DateTime start,DateTime end)=>throw new NotSupportedException();
        public Task<BibleSnapshotState?> GetSnapshotAsync(Guid document,BibleType kind,CancellationToken ct)=>Task.FromResult<BibleSnapshotState?>(null);
        public Task<BibleSnapshotState> UpsertSnapshotAsync(Guid doc,BibleType kind,string text,string hash,BibleRefreshCursor cursor,BibleRefreshStats stats,CancellationToken ct)=>throw new NotSupportedException("No canon writes.");
        public Task<UserEntitlements> GetEntitlementsAsync(string user)=>Task.FromResult(new UserEntitlements(user,"Professional","Professional",new Dictionary<string,string>()));public PlanTier GetUserTier(UserEntitlements e)=>PlanTier.Professional;
        public Task<bool> HasAsync(string user,string key)=>Task.FromResult(true);public Task<int?> GetIntAsync(string user,string key)=>Task.FromResult<int?>(1_000_000);public void InvalidateForUser(string user){}
    }
}
