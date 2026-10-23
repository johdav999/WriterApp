using Microsoft.EntityFrameworkCore;
using System.Net;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DesktopAiWebPersistenceTests
{
    [Theory][InlineData("headers")][InlineData("body")][InlineData("timeout")][InlineData("cancel")][InlineData("invalid-assets")]
    public async Task WebGenerationFailurePreservesPriorConceptsAndAllowsRecoveryWithoutAutomaticRetry(string stage)
    {
        await using var h=new CoverWebHarness();await h.Start();await h.Event("GenerateCoverConceptsAsync");
        Assert.Contains(CoverTestFixture.Image,WebUtility.HtmlDecode(await h.Html()));int before=h.Wire.Generations;
        if(stage=="headers")h.Wire.GenerationFailure=new HttpRequestException("Private transport detail");
        if(stage=="body")h.Wire.InterruptGenerationBody=true;
        if(stage=="timeout")h.Wire.GenerationFailure=new TaskCanceledException("Private transport detail");
        if(stage=="cancel")h.Provider.During=async ct=>{await h.Event("CancelEdit");ct.ThrowIfCancellationRequested();};
        if(stage=="invalid-assets"){h.Wire.BadGenerationAssets=true;h.Provider.Values=[CoverTestFixture.SecondImage];}
        await h.Event("GenerateCoverConceptsAsync");var html=WebUtility.HtmlDecode(await h.Html());
        Assert.Contains(CoverTestFixture.Image,html);Assert.Equal(before+1,h.Wire.Generations);
        Assert.DoesNotContain("Private transport detail",html);
        if(stage is "headers" or "body")Assert.Contains("connection to the cover service was interrupted",html);
        if(stage=="timeout")Assert.Contains("timed out",html);
        if(stage=="cancel"){Assert.Contains("Cover request canceled",html);Assert.DoesNotContain("role=\"alert\"",html);}
        if(stage=="invalid-assets"){Assert.Contains("asset mapping is incomplete",html);Assert.DoesNotContain(CoverTestFixture.SecondImage,html);}
        await using(var db=new WriterApp.Data.AppDbContext(h.Fixture.Options))Assert.Null((await db.Projects.SingleAsync()).CoverImageUrl);
        h.Wire.GenerationFailure=null;h.Wire.InterruptGenerationBody=false;h.Wire.BadGenerationAssets=false;h.Provider.During=null;h.Provider.Values=[CoverTestFixture.SecondImage];
        await h.Event("GenerateCoverConceptsAsync");Assert.Contains(CoverTestFixture.SecondImage,WebUtility.HtmlDecode(await h.Html()));Assert.Contains("Generated 1 cover concept.",await h.Html());
    }
}
