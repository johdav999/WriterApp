using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Data;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;
public sealed class WebAiHistoryUiMigrationTests
{
    [Theory][InlineData("Prepared")][InlineData("Rejected")][InlineData("Confirmed")]
    public async Task DeliveryUiShowsReadableSavedAndUnresolvedStatesWithoutRenderingProviderMarkup(string status) {
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        var rows=new[]{new WebAiHistoryDeliveryItem(Guid.NewGuid(),"Applied","Page",1,status,status=="Confirmed"?"Saved outcome and cloud history confirmed.":"Keep saved writing; reconnect and retry delivery.","<p>Authored 日本語 🧭</p>","<script>alert(1)</script>",Saved:status!="Prepared"),new WebAiHistoryDeliveryItem(Guid.NewGuid(),"Applied","Aggregate",1,"Pending","Reconcile the durable aggregate receipt.",Saved:true)};
        var html=await renderer.Dispatcher.InvokeAsync(async()=> (await renderer.RenderComponentAsync<WebAiHistoryDelivery>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Entries",rows}}))).ToHtmlString());
        Assert.Contains("Retry delivery",html);Assert.Contains(status,html);Assert.Contains("&lt;script&gt;",html);Assert.DoesNotContain("<script>",html);Assert.Contains("cannot be replayed as page HTML",html);
        if(status=="Prepared")Assert.Contains("Apply intent",html);
        var root=Environment.GetEnvironmentVariable("WRITERAPP_P20_EVIDENCE");if(root is not null){Directory.CreateDirectory(Path.Combine(root,"p20"));await File.WriteAllTextAsync(Path.Combine(root,"p20","history-"+status+".html"),html);}
    }
    [Fact]
    public async Task WebHistorySqliteUpgradePreservesWritingSyncDeviceReceiptsAndCreatesOnlyEmptyWebStorage() {
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);await db.Database.OpenConnectionAsync();var migrations=db.GetService<IMigrator>();await migrations.MigrateAsync("20261004090300_OwnedCoverEditProposals");
        var project=Guid.NewGuid();var document=Guid.NewGuid();var section=Guid.NewGuid();db.Projects.Add(new(){Id=project,OwnerUserId="author"});db.Documents.Add(new(){Id=document,ProjectId=project,OwnerUserId="author"});db.Sections.Add(new(){Id=section,DocumentId=document});db.Pages.Add(new(){Id=Guid.NewGuid(),DocumentId=document,SectionId=section,Content="<p><strong>Åsa 日本語 🧭</strong></p>"});
        var device=new WriterApp.Data.AI.DeviceAiHistoryEventRecord {OwnerUserId="author",OperationId=Guid.NewGuid(),LocalEntryId=Guid.NewGuid(),ProposalId=Guid.NewGuid(),DocumentId=document,Sequence=1,State="Applied",RequestHash=new string('A',64),ReportJson="{}",ReceivedAt=DateTimeOffset.UtcNow};db.DeviceAiHistoryEvents.Add(device);await db.SaveChangesAsync();var sync=(await db.DocumentSyncRecords.SingleAsync()).Version;
        await migrations.MigrateAsync();db.ChangeTracker.Clear();Assert.Empty(await db.WebAiHistoryOperations.ToListAsync());Assert.Equal(sync,(await db.DocumentSyncRecords.SingleAsync()).Version);Assert.Equal(device.OperationId,(await db.DeviceAiHistoryEvents.SingleAsync()).OperationId);Assert.Contains("<strong>Åsa 日本語 🧭</strong>",(await db.Pages.SingleAsync()).Content);Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact]
    public void WebHistorySqlServerMigrationAddsOwnedOrderedReceiptsWithoutExecutingExternalDatabase() {
        using var db=new SqlServerMigrationsDbContext(new DbContextOptionsBuilder<SqlServerMigrationsDbContext>().UseSqlServer("Server=localhost;Database=WriterAppScriptOnly;Integrated Security=true;TrustServerCertificate=true").Options);
        var sql=db.GetService<IMigrator>().GenerateScript("20261004090303_OwnedCoverEditProposalsSqlServer",null,MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE [WebAiHistoryOperations]",sql);Assert.Contains("[OwnerUserId] nvarchar(128)",sql);Assert.Contains("CREATE UNIQUE INDEX [IX_WebAiHistoryOperations_OwnerUserId_ApplicationId_Sequence]",sql);Assert.DoesNotContain("CREATE TABLE [DeviceAiHistoryEvents]",sql);Assert.False(db.Database.HasPendingModelChanges());
    }
}
