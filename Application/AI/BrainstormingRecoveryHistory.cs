using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Application.AI;

public sealed class BrainstormingRecoveryHistory(AppDbContext db,string owner,string backend)
{
    private static T Decode<T>(string json)=>JsonSerializer.Deserialize<T>(json,BrainstormingValidation.Json)??throw new InvalidDataException("Recovery record unavailable.");
    public Task<BrainstormingRecoveryBundle> ExchangeAsync(BrainstormingScope scope,BrainstormingRecoveryCopy? copy,CancellationToken ct=default)=>db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>{
        db.ChangeTracker.Clear();await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await db.Database.ExecuteSqlRawAsync("UPDATE DocumentSyncClocks SET Sequence=Sequence WHERE Id=1",ct);
        var session=await new BrainstormingSessions(db,owner,backend).ReadAsync(scope.DocumentId,scope.SessionId,ct)??throw new BrainstormingAccessException();
        BrainstormingValidation.RequireScope(session.Scope,scope);
        var row=await db.BrainstormingSessions.SingleAsync(s=>s.Id==scope.SessionId,ct);
        var copies=row.RecoveryJson is null?new List<BrainstormingRecoveryCopy>():Decode<List<BrainstormingRecoveryCopy>>(row.RecoveryJson);
        if(copy is not null){BrainstormingRecovery.Validate(copy,scope);if(!copies.Any(c=>c.Id==copy.Id))copies.Add(copy);}
        var keeps=await db.BrainstormingKeeps.AsNoTracking().Where(k=>k.SessionId==scope.SessionId).Take(501).ToArrayAsync(ct);
        var planning=await db.BrainstormingPlanningSaves.AsNoTracking().Where(k=>k.SessionId==scope.SessionId).Take(501).ToArrayAsync(ct);
        var result=new BrainstormingRecoveryBundle(1,scope,copies,keeps.Select(k=>new BrainstormingKeepJournal(Decode<BrainstormingKeepReview>(k.ReviewJson),Decode<BrainstormingKeepReceipt>(k.ReceiptJson))).ToArray(),
            planning.Select(k=>new BrainstormingPlanningJournal(Decode<BrainstormingPlanningReview>(k.ReviewJson),Decode<BrainstormingPlanningReceipt>(k.ReceiptJson))).ToArray());
        BrainstormingRecovery.Validate(result,scope);
        if(copy is not null){row.RecoveryJson=BrainstormingPersistence.Json(copies);await db.SaveChangesAsync(ct);}
        await tx.CommitAsync(ct);return result;
    });
}
