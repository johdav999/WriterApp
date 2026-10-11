using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Application.AI;
using WriterApp.Data;
using WriterApp.Shared.Brainstorming;

// Explicit opt-in console probe, never part of ordinary unit tests or application startup.
// Only a fresh GUID-named local SQL Express database can be migrated or removed.
if(args.Length is <1 or >2)throw new ArgumentException("Pass the artifact report path and optionally a proposed SQL script to explicitly run local SQL acceptance.");
var reportPath=Path.GetFullPath(args[0]);
var database="WriterApp_BrainstormingP09_"+Guid.NewGuid().ToString("N");
var connection=new SqlConnectionStringBuilder {DataSource=@".\SQLEXPRESS",InitialCatalog=database,IntegratedSecurity=true,TrustServerCertificate=true,ConnectTimeout=5};
DbContextOptions<SqlServerMigrationsDbContext> Options()=>new DbContextOptionsBuilder<SqlServerMigrationsDbContext>().UseSqlServer(connection.ConnectionString,s=>s.EnableRetryOnFailure()).Options;
void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
var checks=new List<string>();var cleanup=false;var migrated=false;string? failure=null;string? version=null;string[] migrations=[];
await using(var master=new SqlConnection(new SqlConnectionStringBuilder(connection.ConnectionString){InitialCatalog="master"}.ConnectionString)) {
    await master.OpenAsync();await using var command=master.CreateCommand();command.CommandText="SELECT DB_ID(@name)";command.Parameters.AddWithValue("@name",database);
    Require(await command.ExecuteScalarAsync() is DBNull,"Refusing to reuse an existing SQL database.");
}
try {
    await using(var db=new SqlServerMigrationsDbContext(Options())) {
        if(args.Length==2) {
            var chain=db.Database.GetMigrations().ToArray();var first=Array.FindIndex(chain,m=>m.Contains("Brainstorming",StringComparison.Ordinal));
            Require(first>0,"Brainstorm predecessor unavailable.");
            await db.GetService<IMigrator>().MigrateAsync(chain[first-1]);
            var proposed=await File.ReadAllTextAsync(Path.GetFullPath(args[1]));
            Require(!System.Text.RegularExpressions.Regex.IsMatch(proposed,@"\bUSE\s+|\b(?:CREATE|ALTER|DROP)\s+DATABASE\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase),"Proposed script must stay in the disposable database.");
            var batches=System.Text.RegularExpressions.Regex.Split(proposed,@"^\s*GO\s*$",System.Text.RegularExpressions.RegexOptions.Multiline|System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            await using var sql=new SqlConnection(connection.ConnectionString);await sql.OpenAsync();
            for(var replay=0;replay<2;replay++)foreach(var batch in batches.Where(b=>!string.IsNullOrWhiteSpace(b))) {
                await using var command=sql.CreateCommand();command.CommandText=batch;command.CommandTimeout=60;await command.ExecuteNonQueryAsync();
            }
            checks.Add("Exact proposed six-migration SQL script applies after baseline and repeats idempotently in a fresh local database");
        } else await db.Database.MigrateAsync();
        migrated=true;migrations=(await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Require(migrations.Contains("20261007154719_BrainstormingRecoveryHistorySqlServer"),"Recovery migration missing.");
        Require(migrations.Contains("20261007164539_BrainstormingTranscriptionsSqlServer"),"Matched later migration missing.");
        checks.Add("Full matched SQL Server migration chain, including nullable RecoveryJson");
    }
    var document=Guid.NewGuid();var project=Guid.NewGuid();var section=Guid.NewGuid();var page=Guid.NewGuid();
    const string html="<p><strong>Åsa 日本語 🧭</strong> — retained SQL acceptance writing.</p>";
    var server=new BrainstormingScope(1,Guid.NewGuid(),document,null,BrainstormingIdentitySpace.Server,BrainstormingValidation.Fingerprint("https://test.invalid/"),BrainstormingValidation.Fingerprint("author"),null);
    var initial=BrainstormingPersistence.Empty(server) with{State=BrainstormingConversationState.Saved};var write=new BrainstormingWrite(1,Guid.NewGuid(),0,initial);
    BrainstormingSessions Service(AppDbContext db)=>new(db,"author",server.BackendKey);
    BrainstormingRecoveryHistory History(AppDbContext db)=>new(db,"author",server.BackendKey);
    BrainstormingReceipt receipt;
    await using(var db=new SqlServerMigrationsDbContext(Options())) {
        db.Projects.Add(new(){Id=project,OwnerUserId="author",Title="Disposable SQL acceptance"});db.Documents.Add(new(){Id=document,ProjectId=project,OwnerUserId="author",Title="Standalone SQL manuscript"});
        db.Sections.Add(new(){Id=section,DocumentId=document,Title="Section"});db.Pages.Add(new(){Id=page,DocumentId=document,SectionId=section,Content=html});await db.SaveChangesAsync();
        receipt=await Service(db).WriteAsync(write,default);Require(receipt==await Service(db).WriteAsync(write,default),"Duplicate write receipt changed.");
    }
    await using(var db=new SqlServerMigrationsDbContext(Options())) {
        Require(receipt==await Service(db).InspectAsync(document,server.SessionId,write.OperationId,default),"Restart receipt lost.");
        Require(await db.BrainstormingSessionOperations.CountAsync()==1,"Duplicate session write persisted.");
    }
    checks.Add("Session write/inspection deduplicates across a fresh SQL context without replay");
    BrainstormingWrite Branch(string text)=>new(1,Guid.NewGuid(),1,initial with{Revision=2,UpdatedAt=DateTimeOffset.UtcNow,Turns=[new(1,Guid.NewGuid(),server,1,null,Guid.NewGuid(),null,null,BrainstormingRole.User,BrainstormingTurnState.Accepted,text,DateTimeOffset.UtcNow)]});
    async Task<bool> Compete(BrainstormingWrite branch) {
        await using var db=new SqlServerMigrationsDbContext(Options());
        try{await Service(db).WriteAsync(branch,default);return true;}catch(BrainstormingConcurrencyException e){Require(e.Current?.Scope==server,"Conflict lost original server scope.");return false;}
    }
    var races=await Task.WhenAll(Compete(Branch("Retained branch A")),Compete(Branch("Retained branch B")));
    Require(races.Count(r=>r)==1,"SQL concurrency did not retain exactly one accepted branch and one conflict.");checks.Add("Concurrent SQL session CAS accepts one branch and returns the other as a retained conflict");
    var local=server with{Space=BrainstormingIdentitySpace.Local,DocumentId=Guid.NewGuid()};
    var localCache=new BrainstormingCache(1,1,BrainstormingPersistence.ReScope(initial,local),"Private unsent composer",new(1,local,server,1,DateTimeOffset.UtcNow),null,null,[],[],DraftEdit:new(1,Guid.NewGuid(),BrainstormingValidation.Fingerprint(html),"<p>Retained authored proposal.</p>","Retained authored proposal.",DateTimeOffset.UtcNow));
    var copy=BrainstormingRecovery.Copy(localCache)!;
    async Task<BrainstormingRecoveryBundle> Exchange(){await using var db=new SqlServerMigrationsDbContext(Options());return await History(db).ExchangeAsync(server,copy);}
    var bundles=await Task.WhenAll(Exchange(),Exchange());Require(bundles.All(b=>b.Copies.Count==1),"Recovery copies did not deduplicate.");
    Require(bundles.All(b=>b.ServerKeeps.Count==0&&b.ServerPlanning.Count==0),"Client report manufactured server commit history.");
    Require(!BrainstormingPersistence.Json(bundles[0]).Contains(localCache.Composer,StringComparison.Ordinal),"Private composer entered transferred recovery.");
    await using(var db=new SqlServerMigrationsDbContext(Options())) {
        Require(BrainstormingPersistence.Json(bundles[0])==BrainstormingPersistence.Json(await History(db).ExchangeAsync(server,copy)),"Recovery history changed after restart.");
        try{await new BrainstormingRecoveryHistory(db,"foreign",server.BackendKey).ExchangeAsync(server,copy);throw new InvalidDataException("Foreign account accepted.");}catch(BrainstormingAccessException){}
        Require(await db.BrainstormingKeeps.CountAsync()==0&&await db.BrainstormingPlanningSaves.CountAsync()==0,"Unapproved application persisted.");
        Require((await db.Pages.AsNoTracking().SingleAsync()).Content==html,"SQL checks changed writing.");Require(await db.AiActionHistoryEntries.CountAsync()==0,"SQL review created application history.");
        await db.Database.OpenConnectionAsync();await using var command=db.Database.GetDbConnection().CreateCommand();
        command.CommandText="SELECT CONVERT(varchar(100),SERVERPROPERTY('ProductVersion'))";version=(string?)await command.ExecuteScalarAsync();
        command.CommandText="SELECT COUNT(*) FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id WHERE t.name IN ('BrainstormingKeeps','BrainstormingPlanningSaves') AND i.is_unique=1 AND i.is_primary_key=0";
        Require(Convert.ToInt32(await command.ExecuteScalarAsync())>=2,"Unique candidate indexes missing.");
    }
    checks.Add("Concurrent recovery exchange/restart retains one immutable authored copy, original namespace and no fabricated server receipt");
    checks.Add("Foreign account rejected; original rich writing/history untouched; live unique candidate indexes present");
    await ConversationAcceptance.Run(connection.ConnectionString,checks);
}catch(Exception e){failure=e.GetType().Name+": "+e.Message;throw;}
finally {
    // The random name was proved absent before this run; only this exact owned database is removed.
    Require(connection.InitialCatalog==database&&database.StartsWith("WriterApp_BrainstormingP09_",StringComparison.Ordinal),"Unsafe cleanup database.");
    SqlConnection.ClearAllPools();await using(var db=new SqlServerMigrationsDbContext(Options())){await db.Database.EnsureDeletedAsync();cleanup=true;}
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
    await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(new{scope="Local SQL Express migrations/session CAS/recovery/index/conversation retry probe with synthetic provider; not physical device exchange, Keep/planning application or deployed provider acceptance",utc=DateTimeOffset.UtcNow,server=@".\SQLEXPRESS",version,database,migrated,migrations,checks,cleanup,failure},new JsonSerializerOptions{WriteIndented=true}));
}
Console.WriteLine($"SQL acceptance passed: {checks.Count} groups; disposable database removed; report {reportPath}");
