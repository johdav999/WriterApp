using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Collaboration;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class CollaborationConversionTests
{
    [CollaborationLoadFact]
    public async Task MeasuredHundredPageConsistentExportRetainsHundredThousandWords()
    {
        string content = "<p>" + string.Join(' ', Enumerable.Range(0,1000).Select(i=>"word"+i)) + "</p>";
        Guid section = (await _db.Pages.AsNoTracking().FirstAsync()).SectionId;
        await _db.Pages.ExecuteUpdateAsync(p=>p.SetProperty(x=>x.Content,content));
        for(int i=2;i<100;i++)_db.Pages.Add(new(){Id=Guid.NewGuid(),DocumentId=_document,SectionId=section,OrderIndex=i,Content=content});
        await _db.SaveChangesAsync();_db.ChangeTracker.Clear();_version=(await _db.DocumentSyncRecords.AsNoTracking().SingleAsync()).Version;
        var clock=Stopwatch.StartNew();await Convert();long migrationMs=clock.ElapsedMilliseconds;
        clock.Restart();var read=await CollaborationProjectionService.Read(_db,"owner",_document,new(),default);long readMs=clock.ElapsedMilliseconds;
        Assert.Equal(100,read.Pages.Length);Assert.Equal(100000,read.Pages.Sum(p=>p.Text.Split(' ',StringSplitOptions.RemoveEmptyEntries).Length));
        var vector=new CollaborationReadRequest(read.StructureRevision,read.Pages.Select(p=>new CollaborationWatermark(p.PageId,p.Generation,p.Sequence)).ToArray());
        using var server=Api();using var client=server.CreateClient();clock.Restart();
        var response=await client.PostAsJsonAsync($"/api/documents/{_document}/export/print",new WriterApp.Application.Exporting.ExportDocumentRequest(_document,"html",null,"document",Collaboration:vector));
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);string html=await response.Content.ReadAsStringAsync();long exportMs=clock.ElapsedMilliseconds;
        Assert.Equal(100,Regex.Matches(html,@"\bword0\b").Count);Assert.Equal(100,Regex.Matches(html,@"\bword999\b").Count);
        string directory=Path.Combine(new Host().ContentRootPath,"artifacts/realtime-p10");Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory,"manuscript-"+(_database is null?"sqlite":"sqlserver")+".json"),JsonSerializer.Serialize(new{pages=100,words=100000,migrationMs,consistentReadMs=readMs,htmlExportMs=exportMs,exportCharacters=html.Length,scope="Synthetic local migration/read/HTTP HTML export; no provider AI or physical-device acceptance",productionReady=false}));
    }
}
