using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data.Collaboration;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed class CollaborationLoadFactAttribute : FactAttribute
{
    public CollaborationLoadFactAttribute() { if (Environment.GetEnvironmentVariable("WRITERAPP_COLLAB_LOAD_TESTS") != "1") Skip = "Set WRITERAPP_COLLAB_LOAD_TESTS=1 for the owned 100-room/20-editor benchmark."; }
}
public sealed partial class CollaborationSessionTests
{
    private object? _operationsWorkload;
    private bool _loadConnectionPool;
    [CollaborationLoadFact]
    public async Task MeasuredHundredRoomsAndTwentyEditorsPreserveDurabilityAcrossProxyReconnect()
    {
        _clock.RealTime = true; _settings.CurrentValue.CommitsEnabled = true; _sharingOptions.CurrentValue.Enabled = true;
        // Match the application's normal SQLite pooling; ordinary fault fixtures deliberately
        // disable pooling for process/file cleanup and are not a representative load setup.
        if (_sql is null) { _loadConnectionPool = true; _sqlite = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<WriterApp.Data.AppDbContext>().UseSqlite($"Data Source={_file};Pooling=True").Options; }
        var start = new ProcessStartInfo("node") { WorkingDirectory = FindRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("WriterApp.Collaboration/tests/performance-fixture.mjs");
        using var fixture = Process.Start(start)!; string json = await fixture.StandardOutput.ReadToEndAsync(); await fixture.WaitForExitAsync(); Assert.Equal(0, fixture.ExitCode);
        using var metadata = JsonDocument.Parse(json); string fingerprint = metadata.RootElement.GetProperty("support").GetProperty("schemaFingerprint").GetString()!; byte[] seed = metadata.RootElement.GetProperty("state").GetBytesFromBase64();
        var rooms = new List<object> { new { documentId = _document, pageId = _page, generation = _generation } };
        await using (var db = Open())
        {
            var ids = new List<Guid>();
            for (int i=1;i<100;i++)
            {
                Guid document=Guid.NewGuid(),page=Guid.NewGuid(),section=Guid.NewGuid(),generation=Guid.NewGuid(),project=Guid.NewGuid();ids.Add(document);
                db.Projects.Add(new(){Id=project,OwnerUserId="owner"});
                db.Documents.Add(new(){Id=document,ProjectId=project,OwnerUserId="owner"});db.Sections.Add(new(){Id=section,DocumentId=document});db.Pages.Add(new(){Id=page,DocumentId=document,SectionId=section,Content="<p>Owned load source</p>"});
                rooms.Add(new {documentId=document,pageId=page,generation});
                db.CollaborationPages.Add(new(){DocumentId=document,PageId=page,Generation=generation,ContentContractVersion=2,ProtocolVersion=1,SchemaFingerprint=fingerprint});
                db.CollaborationSnapshots.Add(new(){PageId=page,Generation=generation,Sequence=0,State=seed,Sha256=Hash(seed)});
            }
            for(int i=0;i<20;i++)db.CollaborationMembers.Add(new(){DocumentId=_document,UserId="editor-"+i,Role=CollaborationRole.Editor});
            await db.SaveChangesAsync();await db.Documents.Where(d=>ids.Contains(d.Id)).ExecuteUpdateAsync(d=>d.SetProperty(x=>x.CollaborationMode,CollaborationMode.Collaborative));
            await db.CollaborationPages.Where(p=>p.PageId==_page).ExecuteUpdateAsync(p=>p.SetProperty(x=>x.SchemaFingerprint,fingerprint));
            await db.CollaborationSnapshots.Where(s=>s.PageId==_page).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,seed).SetProperty(x=>x.Sha256,Hash(seed)));
        }
        _operationsWorkload = new { rooms, provider = _database is null ? "sqlite" : "sqlserver" };
        await RunProbe("operations-probe.mjs", Request with { SchemaFingerprint = fingerprint, RequestCommit = true });
        await using var inspect = Open(); Assert.Equal(100, await inspect.CollaborationReceipts.CountAsync()); Assert.Equal(100, (await inspect.CollaborationPages.SingleAsync(p=>p.PageId==_page)).CommittedSequence);
    }
}
