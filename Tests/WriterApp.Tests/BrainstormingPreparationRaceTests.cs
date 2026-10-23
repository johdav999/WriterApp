using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingPreparationRaceTests
{
    private sealed class RaceStore(LocalBrainstormingStore inner,Func<BrainstormingCache,BrainstormingCache> race):IBrainstormingCacheStore
    {
        private bool raced;
        public Task<BrainstormingCache?> ReadAsync(BrainstormingScope scope,CancellationToken ct=default)=>inner.ReadAsync(scope,ct);
        public async Task<BrainstormingCache> WriteAsync(BrainstormingCache value,long expected,Guid operation,CancellationToken ct=default) {
            if(!raced){raced=true;var current=(await inner.ReadAsync(value.Session.Scope,ct))!;await inner.WriteAsync(race(current),current.CacheRevision,Guid.NewGuid(),ct);}
            return await inner.WriteAsync(value,expected,operation,ct);
        }
        public Task<IReadOnlyList<BrainstormingSummary>> ListAsync(BrainstormingScope scope,Guid? after=null,int limit=25,CancellationToken ct=default)=>inner.ListAsync(scope,after,limit,ct);
        public Task<IReadOnlyList<BrainstormingRejectedWrite>> ReadRejectedAsync(BrainstormingScope scope,CancellationToken ct=default)=>inner.ReadRejectedAsync(scope,ct);
    }
    private static async Task<(LocalBrainstormingStore Store,BrainstormingSession Session,BrainstormingPreparation Preparation)> Fixture()
    {
        var session=BrainstormingContractTests.Fixture();var store=new LocalBrainstormingStore(Path.Combine(Path.GetTempPath(),"BrainstormingPreparationRace",Guid.NewGuid().ToString("N")));
        await store.WriteAsync(new(1,1,session,"",null,null,null,[],[]),0,Guid.NewGuid());
        var preparation=new BrainstormingPreparation(1,new(1,session.Targets[0] with {Id=Guid.NewGuid()},null,[],[],[]),session.Contexts[0] with {Id=Guid.NewGuid()},[]);
        return(store,session,preparation);
    }
    [Fact]
    public async Task TypingDuringContextSaveRetainsMessageAndAcceptsPreparation()
    {
        var f=await Fixture();var service=new BrainstormingSessionService(new RaceStore(f.Store,current=>current with {Composer="My unsent message. 日本語 🧭"}),null!);
        var saved=await service.RememberPreparationAsync(f.Session.Scope,f.Preparation);
        Assert.Equal("My unsent message. 日本語 🧭",saved.Composer);Assert.Contains(saved.Session.Contexts,c=>c.Id==f.Preparation.Context!.Id);
        Assert.Equal(BrainstormingPersistence.Json(f.Session.Turns),BrainstormingPersistence.Json(saved.Session.Turns));Assert.Equal(BrainstormingPersistence.Json(f.Session.Drafts),BrainstormingPersistence.Json(saved.Session.Drafts));
        Assert.Equal(saved.Composer,(await f.Store.ReadAsync(f.Session.Scope))!.Composer);
    }
    [Fact]
    public async Task ContextSaveDuringTypingPreservesBothIndependentUpdates()
    {
        var f=await Fixture();var service=new BrainstormingSessionService(new RaceStore(f.Store,current=>current with {Session=current.Session with {Revision=current.Session.Revision+1,Contexts=[..current.Session.Contexts,f.Preparation.Context!]}}),null!);
        var saved=await service.SaveComposerAsync(f.Session.Scope,"Typed while preparing.");
        Assert.Equal("Typed while preparing.",saved.Composer);Assert.Contains(saved.Session.Contexts,c=>c.Id==f.Preparation.Context!.Id);
    }
    [Fact]
    public async Task CompetingComposerAndConversationChangesRemainExplicitConflicts()
    {
        var f=await Fixture();var service=new BrainstormingSessionService(new RaceStore(f.Store,current=>current with {Composer="Other tab's message"}),null!);
        await Assert.ThrowsAsync<BrainstormingConcurrencyException>(()=>service.SaveComposerAsync(f.Session.Scope,"This tab's message"));Assert.Equal("Other tab's message",(await f.Store.ReadAsync(f.Session.Scope))!.Composer);
        service=new(new RaceStore(f.Store,current=>current with {Session=current.Session with {Revision=current.Session.Revision+1,State=BrainstormingConversationState.Deleted}}),null!);
        await Assert.ThrowsAsync<BrainstormingConcurrencyException>(()=>service.RememberPreparationAsync(f.Session.Scope,f.Preparation));Assert.Equal(BrainstormingConversationState.Deleted,(await f.Store.ReadAsync(f.Session.Scope))!.Session.State);
    }
}
