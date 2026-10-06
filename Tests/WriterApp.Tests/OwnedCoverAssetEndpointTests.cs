using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WriterApp.Application.Covers;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DesktopAiWebPersistenceTests
{
    private static IHost AssetHost(TranslationFixture f,RemoteCoverNetwork network)=>Host($"Data Source={Path.Combine(f.Root,"web.db")};Pooling=False",services=> {
        services.AddScoped<IDeletedUserIdentityService,DeletedUserIdentityService>();
        services.AddSingleton<ICoverAssetNetwork>(network);services.Configure<CoverAssetOptions>(options=>options.TrustedStoragePrefixes=["https://provider.test/owned-container/"]);
        services.AddScoped<TrustedCoverFetcher>();services.AddScoped<CoverAssetService>();
        services.AddSingleton<ICoverImageService>(new CoverTestFixture.Images{Values=[RemoteCoverNetwork.Reference,CoverTestFixture.SecondImage]});
    });
    private static async Task<CoverAssetSource> SavedRemoteSource(TranslationFixture f,string reference=RemoteCoverNetwork.Reference) {
        await using var db=new AppDbContext(f.Options);var project=await db.Projects.SingleAsync();project.CoverImageUrl=reference;await db.SaveChangesAsync();
        var state=await db.DocumentSyncRecords.AsNoTracking().SingleAsync(r=>r.DocumentId==f.Document);
        return new(1,f.Project,f.Document,project.MetadataRevision,state.Version,CoverAssetContract.ReferenceHash(reference));
    }
    [Fact]
    public async Task OwnedProjectCannotFetchAReferenceRegisteredToAnotherOwnerOrProject() {
        await using var f=new TranslationFixture();await f.Start();var source=await SavedRemoteSource(f);await using var db=new AppDbContext(f.Options);
        db.CoverAssets.Add(new(){Id=Guid.NewGuid(),OwnerUserId="foreign-author",ProjectId=Guid.NewGuid(),SourceDocumentId=Guid.NewGuid(),
            ReferenceHash=source.ReferenceHash,RemoteReference=RemoteCoverNetwork.Reference,Bytes=Convert.FromBase64String(CoverTestFixture.Png)});
        await db.SaveChangesAsync();var network=new RemoteCoverNetwork();var service=new CoverAssetService(db,network.Fetcher());
        var denied=await Assert.ThrowsAsync<CoverAssetException>(()=>service.ProjectAsync("ordinary-author",source,default));Assert.Equal(404,denied.Status);Assert.Equal(0,network.Calls);
        Assert.Single(await db.CoverAssets.ToListAsync());
    }
    [Fact]
    public async Task ProductionOwnedEndpointPreservesMetadataWritingAndImmutableBytesAcrossRestartAndProviderGeneration() {
        await using var f=new TranslationFixture();await f.Start();var source=await SavedRemoteSource(f);f.Http.Dispose();f.Server.Dispose();
        var network=new RemoteCoverNetwork();f.Server=AssetHost(f,network);f.Http=f.Server.GetTestClient();f.Http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
        using var anonymous=f.Server.GetTestClient();Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync("api/covers/assets/materialize",source)).StatusCode);
        using var foreign=f.Server.GetTestClient();foreign.DefaultRequestHeaders.Add("X-Test-Owner","other-author");
        Assert.Equal(HttpStatusCode.NotFound,(await foreign.PostAsJsonAsync("api/covers/assets/materialize",source)).StatusCode);Assert.Equal(0,network.Calls);
        Assert.Equal(HttpStatusCode.Conflict,(await f.Http.PostAsJsonAsync("api/covers/assets/materialize",source with{MetadataRevision=0})).StatusCode);Assert.Equal(0,network.Calls);
        var first=(await (await f.Http.PostAsJsonAsync("api/covers/assets/materialize",source)).Content.ReadFromJsonAsync<CoverAssetResponse>())!;
        Assert.Equal(source,first.Source);CoverAssetContract.Validate(first.Asset,first.Bytes,f.Project);Assert.Equal(1,network.Calls);
        f.Http.Dispose();f.Server.Dispose();network.Second=true;f.Server=AssetHost(f,network);f.Http=f.Server.GetTestClient();f.Http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
        var retried=(await (await f.Http.PostAsJsonAsync("api/covers/assets/materialize",source)).Content.ReadFromJsonAsync<CoverAssetResponse>())!;
        Assert.Equal(first.Asset,retried.Asset);Assert.Equal(first.Bytes,retried.Bytes);Assert.Equal(1,network.Calls);
        var prompt=new CoverPrompt{ContractVersion=1,ProjectId=f.Project,DocumentId=f.Document,ExpectedMetadataRevision=source.MetadataRevision,ExpectedDocumentVersion=source.DocumentVersion};
        var generation=await f.Http.PostAsJsonAsync("api/covers/generate",prompt);Assert.Equal(HttpStatusCode.OK,generation.StatusCode);
        var concepts=(await generation.Content.ReadFromJsonAsync<CoverGenerationResponse>())!;
        Assert.Equal(first.Asset,concepts.Assets![0]);Assert.Equal(first.Bytes,CoverStudioContract.ReadPng(concepts.ImageUrls[0]));
        await using var verify=new AppDbContext(f.Options);
        var project=await verify.Projects.SingleAsync();Assert.Equal(RemoteCoverNetwork.Reference,project.CoverImageUrl);Assert.Equal(source.MetadataRevision,project.MetadataRevision);
        Assert.Equal(source.DocumentVersion,(await verify.DocumentSyncRecords.SingleAsync()).Version);
        Assert.Equal(f.Html,(await verify.Pages.ToListAsync()).OrderBy(p=>Array.IndexOf(f.Pages,p.Id)).Select(p=>p.Content));Assert.Equal(2,await verify.CoverAssets.CountAsync());
    }
    [Theory][InlineData("foreign-reference")][InlineData("private-dns")][InlineData("deleted-document")][InlineData("deleted-account")]
    public async Task ProductionEndpointRejectsInvalidAuthorizationAndStorageBeforeFetching(string scenario) {
        await using var f=new TranslationFixture();await f.Start();var source=await SavedRemoteSource(f,scenario=="foreign-reference" ? "https://foreign.test/cover.png" : RemoteCoverNetwork.Reference);
        await using(var db=new AppDbContext(f.Options)) {
            if(scenario=="deleted-document")(await db.Documents.SingleAsync()).DeletedAtUtc=DateTime.UtcNow;
            if(scenario=="deleted-account")db.DeletedUserIdentities.Add(new(){UserId="ordinary-author"});
            await db.SaveChangesAsync();
        }
        f.Http.Dispose();f.Server.Dispose();var network=new RemoteCoverNetwork();if(scenario=="private-dns")network.Addresses=[IPAddress.Loopback];
        f.Server=AssetHost(f,network);f.Http=f.Server.GetTestClient();f.Http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
        var response=await f.Http.PostAsJsonAsync("api/covers/assets/materialize",source);
        Assert.False(response.IsSuccessStatusCode);Assert.Equal(0,network.Calls);
        await using var verify=new AppDbContext(f.Options);Assert.Empty(await verify.CoverAssets.ToListAsync());Assert.Equal(source.MetadataRevision,(await verify.Projects.SingleAsync()).MetadataRevision);
    }
}
