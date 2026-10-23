using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Search;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class CollaborationConversionTests
{
    [Fact]
    public async Task SharedImageUploadUsesManuscriptOwnerAndRejectsViewerAndRevoked()
    {
        await Convert();await InviteMember("editor",CollaborationRole.Editor);
        string root=Path.Combine(Path.GetTempPath(),"WriterApp_SharingAssets_"+Guid.NewGuid().ToString("N"));
        byte[] bytes=[137,80,78,71,13,10,26,10];
        try {
            using var api=Api(true,root);using var guest=api.CreateClient();using var owner=api.CreateClient();
            guest.DefaultRequestHeaders.Add("X-Test-Owner","editor");owner.DefaultRequestHeaders.Add("X-Test-Owner","owner");
            async Task<HttpResponseMessage> Upload() {
                using var body=new MultipartFormDataContent();var file=new ByteArrayContent(bytes);file.Headers.ContentType=new("image/png");body.Add(file,"file","test.png");
                return await guest.PostAsync($"/api/documents/{_document}/assets/images",body);
            }
            using var upload=await Upload();Assert.Equal(HttpStatusCode.OK,upload.StatusCode);
            var asset=(await upload.Content.ReadFromJsonAsync<WriterApp.Controllers.DocumentImageAssetsController.UploadImageAssetResponse>())!;
            Assert.Equal(bytes,await owner.GetByteArrayAsync(asset.Url));Assert.Equal(bytes,await guest.GetByteArrayAsync(asset.Url));
            await Sharing.Change("owner",_document,"editor",new(1,CollaborationRole.Viewer,false),default);
            using var denied=await Upload();Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
            Assert.Equal(bytes,await guest.GetByteArrayAsync(asset.Url));
            await Sharing.Change("owner",_document,"editor",new(2,CollaborationRole.Viewer,true),default);
            using var revoked=await guest.GetAsync(asset.Url);Assert.Equal(HttpStatusCode.Forbidden,revoked.StatusCode);
        } finally {
            string path=Path.GetFullPath(root);Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()),path,StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("WriterApp_SharingAssets_",Path.GetFileName(path));if(Directory.Exists(path))Directory.Delete(path,true);
        }
    }
    [Fact]
    public async Task SharedSearchAndAssetRoutesCannotExposePrivateSiblingsAndRevocationCutsBoth()
    {
        var support = await SupportingFixtures();
        var project = await _db.Documents.Where(d => d.Id == _document).Select(d => d.ProjectId).SingleAsync();
        var privatePage = await _db.Pages.AsNoTracking().SingleAsync(p => p.DocumentId == support.Private);
        var linkedPage = await _db.Pages.AsNoTracking().SingleAsync(p => p.DocumentId == support.Linked);
        var normalIndex = new SearchIndexService(_db, NullLogger<SearchIndexService>.Instance, new SearchIndexBackfillQueue());
        await normalIndex.UpsertPageAsync(privatePage, default); await normalIndex.UpsertPageAsync(linkedPage, default);
        await Convert(); await InviteMember("viewer", CollaborationRole.Viewer);
        await normalIndex.UpsertPageAsync(await _db.Pages.AsNoTracking().SingleAsync(p => p.Id == _page), default);
        var index = new SearchIndexService(_db, NullLogger<SearchIndexService>.Instance, new SearchIndexBackfillQueue(), Access);
        Assert.NotEmpty(await index.SearchAsync("viewer", project, "Bold", true, 50, null, default));
        Assert.Empty(await index.SearchAsync("viewer", project, "PRIVATE SUPPORTING SECRET", true, 50, null, default));
        Assert.Empty(await index.SearchAsync("viewer", project, "Shared supporting fixture", true, 50, null, default));

        string ownedRoot = Path.Combine(Path.GetTempPath(), "WriterApp_SharingAssets_" + Guid.NewGuid().ToString("N"));
        string ownerFolder = System.Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("owner")).AsSpan(0, 12)).ToLowerInvariant();
        Guid asset = Guid.NewGuid(); byte[] bytes = [137, 80, 78, 71, 13, 10, 26, 10];
        foreach (var id in new[] { _document, support.Linked, support.Private })
        { var directory = Path.Combine(ownedRoot, ownerFolder, id.ToString("N")); Directory.CreateDirectory(directory); await File.WriteAllBytesAsync(Path.Combine(directory, asset.ToString("N") + ".png"), bytes); }
        try
        {
            using var api = Api(true, ownedRoot); using var client = api.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-Owner", "viewer");
            string Url(Guid id) => $"/api/documents/{id}/assets/images/{asset}";
            var image = await client.GetAsync(Url(_document)); Assert.Equal(HttpStatusCode.OK, image.StatusCode); Assert.Equal(bytes, await image.Content.ReadAsByteArrayAsync());
            Assert.True(image.Headers.CacheControl!.NoStore);
            var search = await client.GetAsync($"/api/search?projectId={project}&q=Bold"); Assert.Equal(HttpStatusCode.OK, search.StatusCode); Assert.Contains(_document.ToString(), await search.Content.ReadAsStringAsync());
            var privateSearch = await client.GetStringAsync($"/api/search?projectId={project}&q=PRIVATE"); Assert.DoesNotContain("PRIVATE SUPPORTING SECRET", privateSearch);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url(support.Private))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url(support.Linked))).StatusCode);
            await Sharing.Supporting("owner", _document, new(support.Linked), default);
            Assert.NotEmpty(await index.SearchAsync("viewer", project, "Shared supporting fixture", true, 50, null, default));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Url(support.Linked))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url(support.Private))).StatusCode);
            await Sharing.Change("owner", _document, "viewer", new(1, CollaborationRole.Viewer, true), default);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url(_document))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url(support.Linked))).StatusCode);
            Assert.Empty(await index.SearchAsync("viewer", project, "Bold", true, 50, null, default));
        }
        finally
        {
            string path = Path.GetFullPath(ownedRoot), parent = Path.GetFullPath(Path.GetTempPath());
            Assert.StartsWith(parent, path, StringComparison.OrdinalIgnoreCase); Assert.StartsWith("WriterApp_SharingAssets_", Path.GetFileName(path));
            Directory.Delete(path, recursive: true);
        }
    }
}
