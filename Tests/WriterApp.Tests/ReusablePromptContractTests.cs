using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;
public sealed class ReusablePromptContractTests
{
    [Fact]
    public void UnicodeDefinitionsRoundTripWithinTheDeclaredFieldLimitsAndAmbiguousPropertiesAreRejected() {
        var p=ReusablePromptTransferTests.Custom() with{Name=new string('日',100),Category=new string('日',100),Template=new string('日',2000),Parameters=Enumerable.Range(0,9).ToDictionary(i=>"variable_"+i,i=>(object?)new string('日',365))};
        var serialized=ReusablePrompts.Serialize(p);Assert.True(serialized.Length>32_000);Assert.Equal(ReusablePrompts.Canonical(p),ReusablePrompts.Canonical(ReusablePrompts.Parse(serialized)));
        Assert.Throws<InvalidDataException>(()=>ReusablePrompts.Parse(serialized.Insert(1,"\"Name\":\"Ambiguous\",")));
    }
    [Theory][InlineData("rewrite.selection",WritingScope.Selection)][InlineData("expand.selection",WritingScope.Selection)][InlineData("tighten.section",WritingScope.Section)]
    [InlineData("change_tone.selection",WritingScope.Section)][InlineData("show_dont_tell.section",WritingScope.Selection)]
    public void BuiltinDefinitionResolvesDeclaredScopeWithoutDiscardingParameters(string action,WritingScope scope) {
        var p=new PromptDefinition("Preset","Style","builtin",action,null,action.StartsWith("change_tone")?new(){["tone"]="dramatic"}:new(),scope);
        ReusablePrompts.ValidateForRun(p);Assert.EndsWith(scope==WritingScope.Selection?".selection":".section",ReusablePrompts.Resolve(p));
        Assert.Equal(ReusablePrompts.Canonical(p.Parameters),ReusablePrompts.Canonical(ReusablePrompts.ExecutionParameters(p)));
    }
    [Theory][InlineData("unknown-action")][InlineData("rewrite-section")][InlineData("ignored-tone")][InlineData("wrong-type")][InlineData("missing-token")][InlineData("unused-param")]
    [InlineData("reserved")][InlineData("mismatched-scope")][InlineData("mixed-kind")][InlineData("lenient-tokens")][InlineData("invalid-name")]
    public void UnsupportedCombinationsRemainClearlyUnavailable(string scenario) {
        var p=ReusablePromptTransferTests.Custom();p=scenario switch{
            "unknown-action"=>p with{Kind="builtin",ActionKey="scene.suggest",Template=null},
            "rewrite-section"=>p with{Kind="builtin",ActionKey="rewrite.section",Template=null,Parameters=new()},
            "ignored-tone"=>p with{Kind="builtin",ActionKey="tighten.section",Template=null,Parameters=new(){["tone"]="dramatic"}},
            "wrong-type"=>p with{Parameters=new(){["tone"]=true,["focus"]="Pacing"}},
            "missing-token"=>p with{Parameters=new()},"unused-param"=>p with{Parameters=new(){["unexpected"]="data"}},
            "reserved"=>p with{Template="Revise {writing_structure}",Parameters=new(){["writing_structure"]="overwrite"}},
            "mismatched-scope"=>p with{Parameters=new(){["scope"]="selection"}},"mixed-kind"=>p with{ActionKey="rewrite.selection"},
            "lenient-tokens"=>p with{Parameters=new(){["strictTokens"]=false,["focus"]="Pacing"}},
            _=>p with{Template="Revise {invalid name}",Parameters=new()}};
        Assert.NotNull(ReusablePrompts.Unavailable(p));
    }
    [Fact]
    public async Task ReplayAfterCloudDeletionNeverResurrectsAndReusingOperationWithOtherContentIsRejected() {
        using var f=new PresetTransferFixture();await f.SignIn();var create=new PromptTransferRequest(1,Guid.NewGuid(),Guid.NewGuid(),"upsert",null,ReusablePromptTransferTests.Custom());
        await using(var db=f.Db()){var receipt=Payload(await f.Controller(db).Transfer(create,default));Assert.Equal(create.PresetId,receipt.PresetId);}
        await f.ChangeCloud(create.PresetId,true);
        await using(var db=f.Db()){var replay=Payload(await f.Controller(db).Transfer(create,default));Assert.False(replay.Deleted);Assert.Empty(await db.PromptPresets.ToArrayAsync());}
        await using(var db=f.Db()){var result=await f.Controller(db).Transfer(create with{Preset=create.Preset! with{Name="Overwrite"}},default);Assert.IsType<ConflictObjectResult>(result.Result);Assert.Empty(await db.PromptPresets.ToArrayAsync());}
    }
    [Fact]
    public async Task DeleteRequiresCurrentTokenAndIdenticalDeleteReplayHasOneReceipt() {
        using var f=new PresetTransferFixture();await f.SignIn();var create=new PromptTransferRequest(1,Guid.NewGuid(),Guid.NewGuid(),"upsert",null,ReusablePromptTransferTests.Custom());PromptTransferResponse original;
        await using(var db=f.Db())original=Payload(await f.Controller(db).Transfer(create,default));
        var deletion=new PromptTransferRequest(1,Guid.NewGuid(),create.PresetId,"delete",original.SourceVersion,null);
        await f.ChangeCloud(create.PresetId,false);
        await using(var db=f.Db()){Assert.IsType<ConflictObjectResult>((await f.Controller(db).Transfer(deletion,default)).Result);Assert.Single(await db.PromptPresets.ToArrayAsync());}
        await using(var db=f.Db()){var current=Assert.IsType<PromptPresetDto[]>(Assert.IsType<OkObjectResult>((await f.Controller(db).TransferLibrary(default)).Result).Value).Single();deletion=deletion with{ExpectedVersion=current.Version};Assert.True(Payload(await f.Controller(db).Transfer(deletion,default)).Deleted);}
        await using(var db=f.Db()){Assert.True(Payload(await f.Controller(db).Transfer(deletion,default)).Deleted);Assert.Empty(await db.PromptPresets.ToArrayAsync());Assert.Equal(2,await db.PromptPresetTransfers.CountAsync());}
    }
    [Fact]
    public async Task SecondWriterCannotOverwriteFirstWritersTokenAndLegacyUpdatesAreDetected() {
        using var f=new PresetTransferFixture();await f.SignIn();var create=new PromptTransferRequest(1,Guid.NewGuid(),Guid.NewGuid(),"upsert",null,ReusablePromptTransferTests.Custom());PromptTransferResponse first;
        await using(var db=f.Db())first=Payload(await f.Controller(db).Transfer(create,default));
        var update=create with{OperationId=Guid.NewGuid(),ExpectedVersion=first.SourceVersion,Preset=create.Preset! with{Name="Writer one"}};
        await using(var db=f.Db())Payload(await f.Controller(db).Transfer(update,default));
        await using(var db=f.Db()){var conflict=Assert.IsType<PromptTransferConflict>(Assert.IsType<ConflictObjectResult>((await f.Controller(db).Transfer(update with{OperationId=Guid.NewGuid(),Preset=create.Preset! with{Name="Writer two"}},default)).Result).Value);Assert.Equal("Writer one",conflict.Current!.Name);}
        await using(var db=f.Db()){var p=await db.PromptPresets.SingleAsync();p.TemplateText="Legacy changed template";await db.SaveChangesAsync();}
        await using(var db=f.Db()){Assert.IsType<ConflictObjectResult>((await f.Controller(db).Transfer(create with{OperationId=Guid.NewGuid(),ExpectedVersion=first.SourceVersion},default)).Result);}
    }
    [Fact]
    public async Task ForeignOwnerProjectAndOversizedInvalidRequestsMakeNoMutation() {
        using var f=new PresetTransferFixture();await f.SignIn();var request=new PromptTransferRequest(1,Guid.NewGuid(),Guid.NewGuid(),"upsert",null,ReusablePromptTransferTests.Custom());
        await using(var db=f.Db())Payload(await f.Controller(db).Transfer(request,default));
        await using(var db=f.Db()){Assert.IsType<NotFoundResult>((await f.Controller(db,"other").Transfer(request with{OperationId=Guid.NewGuid()},default)).Result);Assert.Empty(Assert.IsType<PromptPresetDto[]>(Assert.IsType<OkObjectResult>((await f.Controller(db,"other").TransferLibrary(default)).Result).Value));}
        await using(var db=f.Db()){var bad=request with{OperationId=Guid.NewGuid(),PresetId=Guid.NewGuid(),Preset=request.Preset! with{ProjectId=Guid.NewGuid()}};Assert.IsType<NotFoundResult>((await f.Controller(db).Transfer(bad,default)).Result);}
        await using(var db=f.Db()){Assert.IsType<BadRequestObjectResult>((await f.Controller(db).Transfer(request with{Version=99},default)).Result);Assert.IsType<BadRequestObjectResult>((await f.Controller(db).Transfer(request with{Preset=request.Preset! with{Template=new string('x',2001)}},default)).Result);Assert.Single(await db.PromptPresets.ToArrayAsync());Assert.Single(await db.PromptPresetTransfers.ToArrayAsync());}
    }
    [Fact]
    public async Task InvalidLegacyStoredParametersNeverReplaceTheLastValidCloudCache() {
        using var f=new PresetTransferFixture();await f.SignIn();var local=await f.Store.SavePresetAsync(ReusablePromptTransferTests.Custom(),null,null,f.Library.Scope);await f.Library.SendAsync((await f.Library.QueueCopyAsync(local,false,default)).Id,default);await f.Library.RefreshPresetsAsync(default);
        var original=(await f.Store.CachedPromptsAsync(f.Library.Scope!))!;
        foreach(string malformed in new[]{"null","{broken",new string('x',20_001)}) {
            await using(var db=f.Db()){var p=await db.PromptPresets.SingleAsync();p.ParametersJson=malformed;await db.SaveChangesAsync();}
            await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Library.RefreshPresetsAsync(default));Assert.Equal(ReusablePrompts.Canonical(original),ReusablePrompts.Canonical(await f.Store.CachedPromptsAsync(f.Library.Scope!)));
            await using(var db=f.Db())Assert.Equal(malformed,(await db.PromptPresets.SingleAsync()).ParametersJson);
        }
    }
    [Fact]
    public async Task SqliteUpgradePreservesOldPresetRowsAndBothProviderSnapshotsMatchCurrentModels() {
        using var f=new PresetTransferFixture();string path=Path.Combine(f.Root,"upgrade.db");var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source="+path).Options;
        await using(var db=new AppDbContext(options)){await db.GetService<IMigrator>().MigrateAsync("20261002061051_MultiDocumentProjects");await db.Database.ExecuteSqlRawAsync("INSERT INTO PromptPresets(Id,OwnerUserId,Name,Kind,TemplateText,ParametersJson,CreatedUtc,UpdatedUtc) VALUES ('00000000-0000-0000-0000-000000000001','old-owner','Original','custom','Retained template','{{}}','2026-10-01','2026-10-01')");await db.Database.MigrateAsync();var p=await db.PromptPresets.SingleAsync();Assert.Equal("Retained template",p.TemplateText);Assert.Null(p.Scope);Assert.False(p.Pinned);Assert.False(db.Database.HasPendingModelChanges());}
        await using var sql=new SqlServerMigrationsDbContext(new DbContextOptionsBuilder<SqlServerMigrationsDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true").Options);Assert.False(sql.Database.HasPendingModelChanges());
        var script=sql.GetService<IMigrator>().GenerateScript("20261002061110_MultiDocumentProjectsSqlServer");Assert.Contains("PromptPresetTransfers",script);Assert.Contains("[Scope] int NULL",script);Assert.Contains("[Pinned] bit NOT NULL",script);
    }
    private static PromptTransferResponse Payload(ActionResult<PromptTransferResponse> value)=>Assert.IsType<PromptTransferResponse>(Assert.IsType<OkObjectResult>(value.Result).Value);
}
