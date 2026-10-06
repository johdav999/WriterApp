using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Application.Subscriptions;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class ProjectDowngradeAccessTests
{
    [Fact] public async Task SavingProjectCoverChecksOwnershipAndRevisionIncludingConcurrentWriters() {
        using var f=new CoverTestFixture();await f.Start();await using var first=f.Db();await using var second=f.Db();
        var project=await first.Projects.SingleAsync();await second.Projects.LoadAsync();
        var saved=(await BuildController(first,PlanTier.Professional).SaveProjectCover(project.Id,new(CoverTestFixture.Image,0),default)).Result;
        var dto=Assert.IsType<ProjectDto>(Assert.IsType<OkObjectResult>(saved).Value);Assert.Equal(1,dto.MetadataRevision);
        // The second EF context still holds revision zero: the SQL compare-and-swap must detect this race.
        Assert.IsType<ConflictObjectResult>((await BuildController(second,PlanTier.Professional).SaveProjectCover(project.Id,new("https://covers.example/new.png",0),default)).Result);
        await using(var current=f.Db())Assert.Equal(CoverTestFixture.Image,(await current.Projects.SingleAsync()).CoverImageUrl);
        await using var changed=f.Db();var foreign=await changed.Projects.SingleAsync();foreign.OwnerUserId="other-owner";await changed.SaveChangesAsync();
        await using var final=f.Db();Assert.IsType<NotFoundResult>((await BuildController(final,PlanTier.Professional).SaveProjectCover(project.Id,new(CoverTestFixture.Image,1),default)).Result);
    }
    [Theory][InlineData("javascript:alert(1)")][InlineData("https://user:password@covers.example/image.png")][InlineData("data:image/svg+xml;base64,PHN2Zz4=")]
    public async Task SavingInvalidCoverLeavesMetadataRevisionAndCoverUnchanged(string image) {
        using var f=new CoverTestFixture();await f.Start();await using var db=f.Db();var project=await db.Projects.SingleAsync();
        Assert.IsType<BadRequestObjectResult>((await BuildController(db,PlanTier.Professional).SaveProjectCover(project.Id,new(image,0),default)).Result);
        Assert.Equal(0,project.MetadataRevision);Assert.Null(project.CoverImageUrl);
    }
}
