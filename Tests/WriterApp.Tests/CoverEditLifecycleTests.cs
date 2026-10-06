using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Application.Covers;
using WriterApp.Data;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class CoverEditLifecycleTests
{
    [Theory][InlineData("variation")][InlineData("darker")][InlineData("brighter")][InlineData("cinematic")][InlineData("minimal")]
    public async Task DeviceOwnedEditReviewSelectionExplicitOfflineSaveRestartAndRecovery(string operation) {
        using var f=new CoverTestFixture{OwnedInline=true};await f.Start();f.Provider.Values=[CoverTestFixture.Image];var original=await f.Studio.GenerateAsync(f.Source,f.Brief());
        var draft=await f.Studio.EditAsync(f.Source,original,operation,f.Brief());Assert.Equal(operation,f.Provider.Operation);Assert.Equal(Convert.FromBase64String(CoverTestFixture.Png),f.Provider.Input);
        Assert.Null((await f.Documents.LoadAsync(f.Source.DocumentId))!.Project!.CoverImageUrl);Assert.Equal(0,draft.Selected);Assert.NotNull(draft.Edit);
        f.Network.SetOnline(false);var reopened=(await new LocalCoverStudioStore(f.Root+"/covers").ReadAsync(draft.Scope,f.Source))!;Assert.Equal(draft.Id,reopened.Id);
        draft=await f.Studio.SelectAsync(f.Source,reopened,1);var saved=await f.Studio.SaveAsync(f.Source,draft);Assert.Equal(CoverTestFixture.SecondImage,saved.Project!.CoverImageUrl);Assert.True(saved.Project.HasCoverRecovery);
        var restored=await f.Documents.SetProjectCoverAsync(saved,null,Guid.NewGuid(),restore:true);Assert.Null(restored.Project!.CoverImageUrl);Assert.Equal(System.Text.Json.JsonSerializer.Serialize(f.Source.Sections),System.Text.Json.JsonSerializer.Serialize(restored.Sections));
    }
    [Theory][InlineData("unsupported")][InlineData("deleted")][InlineData("changed-version")][InlineData("late-change")][InlineData("cancel")][InlineData("account")][InlineData("unchanged")]
    public async Task DeviceEditFailurePreservesPreviousPreviewAndSavedCover(string mode) {
        using var f=new CoverTestFixture{OwnedInline=true};await f.Start();f.Provider.Values=[CoverTestFixture.Image];var original=await f.Studio.GenerateAsync(f.Source,f.Brief());using var cancel=new CancellationTokenSource();
        if(mode=="unsupported")f.Provider.Unsupported=true;
        if(mode is "deleted" or "changed-version") {await using var db=f.Db();if(mode=="deleted")await db.CoverAssets.ExecuteDeleteAsync();else await db.DocumentSyncRecords.ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Version,"new-version"));}
        if(mode=="late-change")f.Provider.During=async _=>{await using var db=f.Db();await db.Projects.ExecuteUpdateAsync(s=>s.SetProperty(p=>p.MetadataRevision,1));};
        if(mode=="cancel")f.Provider.During=_=>{cancel.Cancel();return Task.CompletedTask;};
        if(mode=="account")f.Provider.During=async _=>{f.Auth.Id="account-2";await f.Account.SignInAsync();};
        if(mode=="unchanged")f.Provider.Edited=CoverTestFixture.Image;
        await Assert.ThrowsAnyAsync<Exception>(()=>f.Studio.EditAsync(f.Source,original,"darker",f.Brief(),cancel.Token));
        f.Auth.Id="account-1";await f.Account.SignInAsync();Assert.Equal(original.Id,(await f.Studio.CachedAsync(f.Source))!.Id);Assert.Null((await f.Documents.LoadAsync(f.Source.DocumentId))!.Project!.CoverImageUrl);
        if(mode is "unsupported" or "deleted" or "changed-version")Assert.Equal(0,f.Provider.Edits);
    }
    [Theory][InlineData("metadata")][InlineData("source-deleted")][InlineData("proposed-deleted")]
    public async Task DeviceSaveRejectsStaleMetadataOrDeletedOwnedAssets(string mode) {
        using var f=new CoverTestFixture{OwnedInline=true};await f.Start();f.Provider.Values=[CoverTestFixture.Image];var original=await f.Studio.GenerateAsync(f.Source,f.Brief());var edited=await f.Studio.EditAsync(f.Source,original,"brighter",f.Brief());
        await using(var db=f.Db()) {
            if(mode=="metadata")await db.Projects.ExecuteUpdateAsync(s=>s.SetProperty(p=>p.MetadataRevision,2));
            else {Guid id=mode=="source-deleted" ? edited.Edit!.Request.AssetId : edited.Edit!.Proposed.Asset.AssetId;await db.CoverAssets.Where(a=>a.Id==id).ExecuteDeleteAsync();}
        }
        await Assert.ThrowsAnyAsync<Exception>(()=>f.Studio.SaveAsync(f.Source,edited));Assert.Null((await f.Documents.LoadAsync(f.Source.DocumentId))!.Project!.CoverImageUrl);
    }
    private static async Task<(CoverAssetService Service,CoverEditResponse Edit)> Prepare(CoverTestFixture f,AppDbContext db) {
        var service=new CoverAssetService(db,new RemoteCoverNetwork().Fetcher());var source=new CoverAssetSource(1,f.Source.Project!.ServerProjectId!.Value,f.Source.ServerDocumentId!.Value,0,"cover-v1",new string('0',64));
        var original=await service.RegisterInlineAsync("user-1",source,Convert.FromBase64String(CoverTestFixture.Png),default);
        var input=new CoverEditRequest(1,source with{ReferenceHash=CoverAssetContract.ReferenceHash(original.Asset.RemoteReference)},original.Asset.AssetId,original.Asset.ContentHash,"cinematic",f.Brief());
        var proposed=await service.RegisterInlineAsync("user-1",input.Source,Convert.FromBase64String(CoverTestFixture.SecondPng),default);return(service,await service.IssueEditAsync("user-1",input,proposed,default));
    }
    [Fact] public async Task ServerSaveApprovalReceiptReplayRestartRecoveryAndPayloadMismatch() {
        using var f=new CoverTestFixture{OwnedInline=true};await f.Start();await using var db=f.Db();var (service,edit)=await Prepare(f,db);var input=new CoverEditSaveRequest(Guid.NewGuid(),edit,edit.Proposed.Asset.AssetId);
        var saved=await service.SaveEditAsync("user-1",input,default);Assert.Equal("committed",saved.State);Assert.Equal(1,saved.MetadataRevision);Assert.Null(saved.PreviousCoverImageUrl);
        await using var fresh=f.Db();var reopened=new CoverAssetService(fresh,new RemoteCoverNetwork().Fetcher());Assert.Equal(saved,await reopened.ReadSaveAsync("user-1",input.OperationId,default));Assert.Equal(saved,await reopened.SaveEditAsync("user-1",input,default));
        await Assert.ThrowsAsync<CoverAssetException>(()=>reopened.SaveEditAsync("user-1",input with{SelectedAssetId=edit.Request.AssetId},default));
        await Assert.ThrowsAsync<CoverAssetException>(()=>reopened.ReadSaveAsync("other",input.OperationId,default));
        var restored=await reopened.RestoreEditAsync("user-1",input.OperationId,1,default);Assert.Equal("restored",restored.State);Assert.Null(restored.CoverImageUrl);Assert.Equal(restored,await reopened.RestoreEditAsync("user-1",input.OperationId,1,default));
        await using var verify=f.Db();Assert.Null((await verify.Projects.SingleAsync()).CoverImageUrl);Assert.Single(await verify.CoverEditSaves.ToListAsync());
    }
    [Fact] public async Task ServerSaveConflictNeverOverwritesAnotherCoverAndRestoreIsGuarded() {
        using var f=new CoverTestFixture{OwnedInline=true};await f.Start();await using var db=f.Db();var (service,edit)=await Prepare(f,db);var input=new CoverEditSaveRequest(Guid.NewGuid(),edit,edit.Proposed.Asset.AssetId);
        var saved=await service.SaveEditAsync("user-1",input,default);await db.Projects.ExecuteUpdateAsync(s=>s.SetProperty(p=>p.CoverImageUrl,CoverTestFixture.Image).SetProperty(p=>p.MetadataRevision,2));
        await Assert.ThrowsAsync<CoverAssetException>(()=>service.RestoreEditAsync("user-1",input.OperationId,1,default));
        await Assert.ThrowsAsync<CoverAssetException>(()=>service.SaveEditAsync("user-1",input with{OperationId=Guid.NewGuid()},default));
        Assert.Equal(CoverTestFixture.Image,(await db.Projects.AsNoTracking().SingleAsync()).CoverImageUrl);
    }
    [Fact] public async Task ServerRefusesForgedOperationOrProposalReceiptBeforeApproving() {
        using var f=new CoverTestFixture{OwnedInline=true};await f.Start();await using var db=f.Db();var (service,edit)=await Prepare(f,db);
        var forged=edit with{Request=edit.Request with{Operation="minimal"}};
        await Assert.ThrowsAsync<CoverAssetException>(()=>service.SaveEditAsync("user-1",new(Guid.NewGuid(),forged,forged.Proposed.Asset.AssetId),default));
        await Assert.ThrowsAsync<CoverAssetException>(()=>service.SaveEditAsync("user-1",new(Guid.NewGuid(),edit with{ProposalId=Guid.NewGuid()},edit.Proposed.Asset.AssetId),default));
        Assert.Empty(await db.CoverEditSaves.ToListAsync());Assert.Null((await db.Projects.SingleAsync()).CoverImageUrl);
    }
    private sealed class CommitFault:Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor {
        public bool Enabled=true;
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> NonQueryExecutingAsync(System.Data.Common.DbCommand command,
            Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData data,Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,CancellationToken ct=default) {
            if(Enabled && command.CommandText.StartsWith("UPDATE \"Projects\"",StringComparison.Ordinal)) {Enabled=false;throw new IOException("Injected pre-commit storage interruption");}
            return ValueTask.FromResult(result);
        }
    }
    [Fact] public async Task ApprovalSurvivesInterruptedCommitAndFreshServiceRetriesWithoutGeneration() {
        using var f=new CoverTestFixture{OwnedInline=true};await f.Start();CoverEditResponse edit;
        await using(var db=f.Db()) {var prepared=await Prepare(f,db);edit=prepared.Edit;}
        var input=new CoverEditSaveRequest(Guid.NewGuid(),edit,edit.Proposed.Asset.AssetId);var fault=new CommitFault();
        await using(var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source="+f.Root+"/cloud.db;Pooling=False").AddInterceptors(fault).Options)) {
            var service=new CoverAssetService(db,new RemoteCoverNetwork().Fetcher());await Assert.ThrowsAsync<IOException>(()=>service.SaveEditAsync("user-1",input,default));
        }
        await using var reopened=f.Db();var retry=new CoverAssetService(reopened,new RemoteCoverNetwork().Fetcher());Assert.Null((await reopened.Projects.SingleAsync()).CoverImageUrl);
        Assert.Equal("approved",(await retry.ReadSaveAsync("user-1",input.OperationId,default)).State);
        var saved=await retry.SaveEditAsync("user-1",input,default);Assert.Equal("committed",saved.State);Assert.Single(await reopened.CoverEditSaves.ToListAsync());Assert.Equal(0,f.Provider.Edits);
    }
    [Fact] public async Task SqliteUpgradeRetainsCoverAndAssetsAndAddsVersionedRecovery() {
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);await db.Database.OpenConnectionAsync();var migrator=db.GetService<IMigrator>();await migrator.MigrateAsync("20261004072914_OwnedCoverAssets");
        db.Projects.Add(new(){Id=Guid.NewGuid(),OwnerUserId="author",CoverImageUrl=CoverTestFixture.Image});await db.SaveChangesAsync();var revision=(await db.Projects.SingleAsync()).MetadataRevision;
        await migrator.MigrateAsync();Assert.Empty(await db.CoverEditSaves.ToListAsync());Assert.Equal(CoverTestFixture.Image,(await db.Projects.SingleAsync()).CoverImageUrl);Assert.Equal(revision,(await db.Projects.SingleAsync()).MetadataRevision);Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact] public void SqlServerSchemaSupportsRecoveryWithoutExecutingDatabase() {
        using var db=new SqlServerMigrationsDbContext(new DbContextOptionsBuilder<SqlServerMigrationsDbContext>().UseSqlServer("Server=localhost;Database=ScriptOnly;Integrated Security=true;TrustServerCertificate=true").Options);
        string sql=db.GetService<IMigrator>().GenerateScript("20261004072915_OwnedCoverAssetsSqlServer",null,MigrationsSqlGenerationOptions.Idempotent);Assert.Contains("CREATE TABLE [CoverEditSaves]",sql);Assert.Contains("[BeforeCover] nvarchar(max)",sql);Assert.False(db.Database.HasPendingModelChanges());
    }
}
