using Xunit;
using Microsoft.EntityFrameworkCore;

namespace WriterApp.Tests;

public sealed partial class CollaborationSessionTests
{
    private bool _localTestUi;
    [CollaborationBrowserFact]
    public async Task NormalProsaWorkspaceEmailInboxAndLiveEditing()
    {
        _localTestUi=true;_clock.RealTime=true;_settings.CurrentValue.CommitsEnabled=true;
        _sharingOptions.CurrentValue.Enabled=true;_interactions.Enabled=true;
        await using(var db=Open())
        {
            foreach(var sql in WriterApp.Data.Documents.DocumentSyncSchemaV1.Install(db.Database.IsSqlServer()))
                await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(db.Database,sql);
            foreach(var actor in new[]{"owner","editor"}) db.ExternalIdentityLinks.Add(new(){UserId=actor,Issuer="https://fixture.test/",ObjectIdentifier=actor,Subject=actor,EmailAtLinkTime=actor+"@fixture.test"});
            var legacy=Guid.Parse("1396210e-fbac-4714-bb20-37560d6b40bf");var section=Guid.NewGuid();
            var project=Guid.NewGuid(); db.Projects.Add(new(){Id=project,OwnerUserId="owner"});
            db.Documents.Add(new(){Id=legacy,ProjectId=project,OwnerUserId="owner",DocumentKind=WriterApp.Data.Documents.DocumentKind.Manuscript,Title="Normal workspace manuscript"});
            db.Sections.Add(new(){Id=section,DocumentId=legacy,Title="Chapter one"});
            db.Pages.Add(new(){Id=Guid.NewGuid(),DocumentId=legacy,SectionId=section,Title="First page",Content="<p>Existing writing retained.</p>"});
            db.Pages.Add(new(){Id=Guid.Parse("8567c507-26f0-4e69-81c5-6b7676fe11cc"),DocumentId=legacy,SectionId=section,OrderIndex=1,Title="Second page",Content="<p>Second page only.</p>"});
            await db.SaveChangesAsync();
        }
        await using(var db=Open()) {
            foreach(var sql in WriterApp.Data.Collaboration.CollaborationWriteFenceSchema.Install(db.Database.IsSqlServer()))await db.Database.ExecuteSqlRawAsync(sql);
            foreach(var sql in WriterApp.Data.Collaboration.CollaborationStructureSchema.Install(db.Database.IsSqlServer()))await db.Database.ExecuteSqlRawAsync(sql);
        }
        await RunProbe("normal-ui-browser-probe.mjs",Request);
    }
    [CollaborationBrowserFact]
    public async Task LocalTwoAccountTestUiCreatesInvitesEditsDowngradesAndRevokes()
    {
        _localTestUi=true;_clock.RealTime=true;_settings.CurrentValue.CommitsEnabled=true;
        _sharingOptions.CurrentValue.Enabled=true;_interactions.Enabled=true;
        // Match the running migrated database: page/section writes rotate source versions.
        await using(var db=Open())
            foreach(var sql in WriterApp.Data.Documents.DocumentSyncSchemaV1.Install(db.Database.IsSqlServer()))
                await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(db.Database,sql);
        await RunProbe("local-ui-browser-probe.mjs",Request);
    }
}

public sealed partial class CollaborationConversionTests
{
    [Fact]
    public async Task LocalTestLicenseViewDoesNotChangeStoredSubscription()
    {
        var inner=new WriterApp.Application.Subscriptions.UserEntitlementStore(_db,new WriterApp.Application.Usage.SystemClock(),new WriterApp.Application.Security.DeletedUserIdentityService(_db));
        var original=await inner.GetOrCreateAsync("local-license-fixture");
        original.AiTokensUsedThisPeriod=123;await _db.SaveChangesAsync();
        var plan=original.PlanKey;var subscription=original.SubscriptionStatus;
        var local=await new WriterApp.Application.Collaboration.LocalCollaborationEntitlements(inner).GetOrCreateAsync("local-license-fixture");
        Assert.True(WriterApp.Application.Subscriptions.EntitlementAccessEvaluator.Evaluate(local).IsPaidAccessActive);
        Assert.Equal(WriterApp.Application.Subscriptions.UserEntitlementDefaults.StandardMonthlyTokenBudget,local.AiMonthlyTokenBudget);
        Assert.Equal(123,local.AiTokensUsedThisPeriod);Assert.Equal(original.PeriodStartUtc,local.PeriodStartUtc);
        Assert.Equal(plan,original.PlanKey);Assert.Equal(subscription,original.SubscriptionStatus);
        await _db.SaveChangesAsync();_db.ChangeTracker.Clear();
        var persisted=await inner.GetOrCreateAsync("local-license-fixture");
        Assert.Equal(plan,persisted.PlanKey);Assert.Equal(subscription,persisted.SubscriptionStatus);Assert.False(persisted.HasManualPlanOverride);
        Assert.Equal(0,persisted.AiMonthlyTokenBudget);Assert.Equal(123,persisted.AiTokensUsedThisPeriod);
    }
}
