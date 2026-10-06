using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Covers;
using WriterApp.Application.Security;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

internal sealed class CoverTestFixture : IDisposable
{
    internal const string Png="iVBORw0KGgoAAAANSUhEUgAAAFAAAAB4CAIAAADqjOKhAAAA0ElEQVR4nOXOMQEAMAyAMIbySp+L9iAK8mAokRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJkRiJ8Tqw7QPmGwFwSBMZLwAAAABJRU5ErkJggg==";
    internal static string Image => "data:image/png;base64,"+Png;
    internal const string SecondPng="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC";
    internal static string SecondImage => "data:image/png;base64,"+SecondPng;
    public string Root=Path.Combine(Path.GetTempPath(),"WriterApp.Covers",Guid.NewGuid().ToString("N"));
    public FileLocalDocumentStore Store; public LocalDocumentRepository Documents;
    public LocalCoverStudioStore Cache; public DeviceCoverStudio Studio;
    public DeviceConnectivity Network=new(); public Identity Auth=new(); public DeviceAccountService Account;
    public HttpClient Http; public Handler Api; public Images Provider=new(); public LocalDocument Source=null!;
    public DeviceHostOptions Host=new("Test",new Uri("https://cover.test/"));
    public RemoteCoverNetwork? Remote;
    public CoverTestFixture() {
        Store=new(Root+"/docs"); Documents=new(Store); Cache=new(Root+"/covers"); Account=new(Auth); Api=new(this);
        Http=new(new DeviceAuthenticatedHandler(Account,Host.ApiBaseAddress){InnerHandler=Api}){BaseAddress=Host.ApiBaseAddress};
        Studio=new(Http,Account,Network,Host,Documents,Cache);
    }
    public AppDbContext Db()=>new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source="+Root+"/cloud.db;Pooling=False").Options);
    public async Task Start() {
        await Account.SignInAsync(); var doc=await Documents.CreateProjectAsync("Cover project");
        Guid cloudDoc=Guid.NewGuid(), cloudProject=Guid.NewGuid();
        Source=await Store.ApplySyncAsync(doc with { ServerDocumentId=cloudDoc, ServerProjectId=cloudProject,ServerVersion="cover-v1",SyncState=LocalSyncState.Synced,
            Project=doc.Project! with { Version=3,ServerProjectId=cloudProject,ServerMetadataRevision=0,MetadataDirty=false,ServerPrimaryDocumentId=cloudDoc } },doc.LocalRevision,default,projects:true);
        await using var db=Db(); await db.Database.EnsureCreatedAsync(); var now=DateTimeOffset.UtcNow;
        db.Projects.Add(new(){Id=cloudProject,OwnerUserId="user-1",Title="Cloud",CreatedUtc=now,UpdatedUtc=now});
        db.Documents.Add(new(){Id=cloudDoc,ProjectId=cloudProject,OwnerUserId="user-1",Title="Book",CreatedAt=now,UpdatedAt=now});
        db.DocumentSyncRecords.Add(new(){DocumentId=cloudDoc,OwnerUserId="user-1",Version="cover-v1",Sequence=1}); await db.SaveChangesAsync();
    }
    public CoverPrompt Brief()=>new(){Description="A lighthouse",Genre="Mystery",Mood="Dark",Style="Painted",ColorPalette="Cold"};
    public CoverPrompt Checked()=>new(){ContractVersion=1,ProjectId=Source.Project!.ServerProjectId,DocumentId=Source.ServerDocumentId,ExpectedMetadataRevision=0,ExpectedDocumentVersion="cover-v1"};
    public bool OwnedInline;
    public CoversController Controller(AppDbContext db,string owner="user-1")=>new(Provider,NullLogger<CoversController>.Instance,db,new Owner(owner),Remote is null && !OwnedInline ? null : new CoverAssetService(db,(Remote ?? new RemoteCoverNetwork()).Fetcher())) { ControllerContext=new(){HttpContext=new DefaultHttpContext()} };
    public void Dispose(){Http.Dispose(); if(Directory.Exists(Root))Directory.Delete(Root,true);}
    internal sealed class Owner(string owner):IUserIdResolver { public string ResolveUserId(ClaimsPrincipal principal)=>owner; }
    internal sealed class Identity:IDeviceIdentityClient {
        public string Id="account-1";public bool IsConfigured=>true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive,CancellationToken ct)=>Task.FromResult<DeviceAccessToken?>(new("synthetic",DateTimeOffset.UtcNow.AddHours(1),"Writer",Id));
        public Task SignOutAsync()=>Task.CompletedTask;
    }
    internal sealed class Images:ICoverImageService {
        public int Calls; public List<string> Values=[Image,SecondImage]; public Func<CancellationToken,Task>? During;
        public int Edits;public bool Unsupported;public string Edited=SecondImage;public string? Operation;public byte[]? Input;public CoverPrompt? EditBrief;
        public CoverEditCapabilities EditCapabilities=>new(1,Unsupported ? [] : CoverEdits.Operations,Unsupported ? "Synthetic unsupported provider configuration." : null);
        public async Task<string> EditAsync(byte[] image,string operation,CoverPrompt brief,CancellationToken ct=default) {Edits++;Operation=operation;Input=image;EditBrief=brief;if(During is not null)await During(ct);return Edited;}
        public async Task<List<string>> GenerateCoverConceptsAsync(CoverPrompt prompt,CancellationToken ct=default){Calls++; if(During is not null)await During(ct);ct.ThrowIfCancellationRequested();return Values;}
    }
    internal sealed class Handler(CoverTestFixture f):HttpMessageHandler {
        public int Calls;public int? Failure;public bool OldBackend,WrongDocument,Oversize,Interrupted,WrongHash,MissingAsset; public Func<Task>? After;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
            if(request.Method!=HttpMethod.Get)Calls++;if(Failure is { } status)return new((HttpStatusCode)status);
            await using var db=f.Db();var controller=f.Controller(db,f.Account.AccountId=="account-1"?"user-1":"other");
            IActionResult? result;
            if(request.RequestUri!.AbsolutePath.EndsWith("/edit-capabilities"))result=controller.EditCapabilities();
            else if(request.RequestUri!.AbsolutePath.EndsWith("/edit-source"))result=await controller.EditSource((await request.Content!.ReadFromJsonAsync<CoverEditSourceRequest>(ct))!,ct);
            else if(request.RequestUri!.AbsolutePath.EndsWith("/edit"))result=await controller.Edit((await request.Content!.ReadFromJsonAsync<CoverEditRequest>(ct))!,ct);
            else if(request.RequestUri!.AbsolutePath.EndsWith("/materialize")) {
                var source=(await request.Content!.ReadFromJsonAsync<CoverAssetSource>(cancellationToken:ct))!;
                result=await controller.Materialize(source,new DeletedUserIdentityService(db),ct);
            } else {
                var prompt=(await request.Content!.ReadFromJsonAsync<CoverPrompt>(cancellationToken:ct))!;result=(await controller.Generate(prompt,ct)).Result;
            }
            if(After is not null)await After();
            if(result is ObjectResult obj) {
                object? value=obj.Value;
                if(value is CoverGenerationResponse r) { if(OldBackend)value=new CoverGenerationResponse(r.ImageUrls);if(WrongDocument)value=r with{DocumentId=Guid.NewGuid()}; }
                if(value is CoverAssetResponse asset) {
                    if(WrongDocument)value=asset with{Source=asset.Source with{DocumentId=Guid.NewGuid()}};
                    if(WrongHash)value=asset with{Asset=asset.Asset with{ContentHash=new string('0',64)}};
                    if(MissingAsset)value=asset with{Asset=null!};
                }
                HttpContent content=Interrupted ? new InterruptedContent() : JsonContent.Create(value);
                if(Oversize)content.Headers.ContentLength=16*1024*1024+1;
                return new((HttpStatusCode)(obj.StatusCode??200)){Content=content};
            }
            return new(HttpStatusCode.InternalServerError);
        }
    }
    private sealed class InterruptedContent:HttpContent {
        protected override Task SerializeToStreamAsync(Stream stream,System.Net.TransportContext? context)=>throw new IOException("Connection lost.");
        protected override bool TryComputeLength(out long length) { length=0;return false; }
        protected override Task<Stream> CreateContentReadStreamAsync()=>Task.FromResult<Stream>(new InterruptedStream());
    }
    private sealed class InterruptedStream():MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"imageUrls\":[\"data:image/png;base64,")) {
        private bool read;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken ct=default) { if(read)throw new IOException("Interrupted image download.");read=true;return base.ReadAsync(buffer,ct); }
    }
}

public sealed class DeviceCoverStudioTests
{
    [Fact] public async Task GenerateSelectSaveRestartRecoverAndPublishUsesSelectedPngWithoutChangingWriting() {
        using var f=new CoverTestFixture();await f.Start();byte[] original=LocalDocumentCodec.Encode(f.Source);
        var prior=await f.Documents.SetProjectCoverAsync(f.Source,CoverTestFixture.Image,Guid.NewGuid());
        // Model a confirmed ordinary project sync before generating against its new cloud revision.
        await using(var db=f.Db()){var p=await db.Projects.SingleAsync();p.CoverImageUrl=CoverTestFixture.Image;await db.SaveChangesAsync();}
        f.Source=await f.Store.ApplySyncAsync(prior with{SyncState=LocalSyncState.Synced,Project=prior.Project! with{MetadataDirty=false,ServerMetadataRevision=1}},prior.LocalRevision,default,projects:true,overwriteProjectMetadata:true);
        var draft=await f.Studio.GenerateAsync(f.Source,f.Brief());
        Assert.Equal(CoverTestFixture.Image,(await f.Documents.LoadAsync(f.Source.DocumentId))!.Project!.CoverImageUrl);
        draft=await f.Studio.SelectAsync(f.Source,draft,1);Assert.Equal(1,(await new LocalCoverStudioStore(f.Root+"/covers").ReadAsync(draft.Scope,f.Source))!.Selected);
        f.Network.SetOnline(false);var saved=await f.Studio.SaveAsync(f.Source,draft);
        var reopened=(await new LocalDocumentRepository(new FileLocalDocumentStore(f.Root+"/docs")).LoadAsync(saved.DocumentId))!;
        Assert.True(reopened.Project!.HasCoverRecovery);Assert.Equal(CoverTestFixture.Image,reopened.Project.PreviousCoverImageUrl);Assert.Equal(draft.Id,reopened.Project.CoverChangeId);
        Assert.Equal(JsonSerializer.Serialize(f.Source.Sections),JsonSerializer.Serialize(reopened.Sections));Assert.Equal(f.Source.LocalRevision,reopened.LocalRevision);Assert.Equal(LocalSyncState.PendingUpload,reopened.SyncState);
        var publishing=await DevicePublishing.PrepareAsync(reopened,new(DeviceExportFormat.Epub,IncludeCover:true));
        using(var zip=new System.IO.Compression.ZipArchive(new MemoryStream(publishing.File.Content))) { var entry=Assert.Single(zip.Entries,e=>e.FullName.EndsWith("cover.png"));using var bytes=new MemoryStream();await entry.Open().CopyToAsync(bytes);Assert.Equal(Convert.FromBase64String(CoverTestFixture.SecondPng),bytes.ToArray()); }
        var html=await DevicePublishing.PrepareAsync(reopened,new(DeviceExportFormat.Html,IncludeCover:true));Assert.Contains(CoverTestFixture.SecondPng,html.PreviewHtml);Assert.DoesNotContain(CoverTestFixture.Png,html.PreviewHtml);
        var word=await DevicePublishing.PrepareAsync(reopened,new(DeviceExportFormat.Docx,IncludeCover:true));using(var package=DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new MemoryStream(word.File.Content),false)) { var entry=Assert.Single(package.MainDocumentPart!.ImageParts);using var bytes=new MemoryStream();await entry.GetStream().CopyToAsync(bytes);Assert.Equal(Convert.FromBase64String(CoverTestFixture.SecondPng),bytes.ToArray()); }
        var repeated=await f.Studio.SaveAsync(f.Source,draft);Assert.Equal(saved.Project!.MetadataRevision,repeated.Project!.MetadataRevision);
        var restored=await f.Documents.SetProjectCoverAsync(reopened,null,Guid.NewGuid(),restore:true);Assert.Equal(CoverTestFixture.Image,restored.Project!.CoverImageUrl);
        Assert.True(original.Length>0);
    }
    [Theory][InlineData(402)][InlineData(429)][InlineData(503)] public async Task ProviderFailuresPreservePriorCoverAndCache(int status) {
        using var f=new CoverTestFixture();await f.Start();var draft=await f.Studio.GenerateAsync(f.Source,f.Brief());f.Api.Failure=status;
        await Assert.ThrowsAsync<IOException>(()=>f.Studio.GenerateAsync(f.Source,f.Brief()));Assert.Equal(draft.Id,(await f.Studio.CachedAsync(f.Source))!.Id);Assert.Null((await f.Documents.LoadAsync(f.Source.DocumentId))!.Project!.CoverImageUrl);
    }
    [Theory][InlineData("https://remote.invalid/cover.png")][InlineData("data:image/svg+xml;base64,PHN2Zz4=")][InlineData("data:image/png;base64,aGVsbG8=")]
    public async Task InvalidOrRemoteMediaDoesNotReplaceCache(string invalid) {
        using var f=new CoverTestFixture();await f.Start();var draft=await f.Studio.GenerateAsync(f.Source,f.Brief());f.Provider.Values=[invalid];
        await Assert.ThrowsAsync<IOException>(()=>f.Studio.GenerateAsync(f.Source,f.Brief()));Assert.Equal(draft.Id,(await f.Studio.CachedAsync(f.Source))!.Id);Assert.Equal(2,f.Api.Calls);
    }
    [Fact] public async Task StaleDocumentMetadataAndAccountCannotSaveOrExposeDrafts() {
        using var f=new CoverTestFixture();await f.Start();var draft=await f.Studio.GenerateAsync(f.Source,f.Brief());
        var renamed=await f.Documents.RenameAsync(f.Source,"Changed");await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Studio.SaveAsync(renamed,draft));
        f.Auth.Id="account-2";await f.Account.SignInAsync();Assert.Null(await f.Studio.CachedAsync(renamed));await Assert.ThrowsAsync<DeviceSignInRequiredException>(()=>f.Studio.SaveAsync(renamed,draft));
        Assert.Null((await f.Documents.LoadAsync(renamed.DocumentId))!.Project!.CoverImageUrl);
    }
    [Theory][InlineData(true)][InlineData(false)] public async Task BackendMustConfirmExactSourceAndContract(bool old) {
        using var f=new CoverTestFixture();await f.Start();f.Api.OldBackend=old;f.Api.WrongDocument=!old;
        await Assert.ThrowsAsync<InvalidDataException>(()=>f.Studio.GenerateAsync(f.Source,f.Brief()));Assert.Null(await f.Studio.CachedAsync(f.Source));
    }
    [Fact] public async Task CancellationAndAccountSwitchDuringGenerationPreservePriorCache() {
        using var f=new CoverTestFixture();await f.Start();var draft=await f.Studio.GenerateAsync(f.Source,f.Brief());using var ct=new CancellationTokenSource();
        f.Provider.During=token=>{ct.Cancel();return Task.CompletedTask;};await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>f.Studio.GenerateAsync(f.Source,f.Brief(),ct.Token));
        f.Provider.During=null;f.Api.After=async()=>{f.Auth.Id="account-2";await f.Account.SignInAsync();};await Assert.ThrowsAnyAsync<Exception>(()=>f.Studio.GenerateAsync(f.Source,f.Brief()));
        f.Api.After=null;f.Auth.Id="account-1";await f.Account.SignInAsync();Assert.Equal(draft.Id,(await f.Studio.CachedAsync(f.Source))!.Id);Assert.Null((await f.Documents.LoadAsync(f.Source.DocumentId))!.Project!.CoverImageUrl);
    }
    [Theory][InlineData(true)][InlineData(false)] public async Task OversizeAndInterruptedResponseBodiesPreservePreviousConcepts(bool oversize) {
        using var f=new CoverTestFixture();await f.Start();var draft=await f.Studio.GenerateAsync(f.Source,f.Brief());f.Api.Oversize=oversize;f.Api.Interrupted=!oversize;
        if(oversize) await Assert.ThrowsAsync<InvalidDataException>(()=>f.Studio.GenerateAsync(f.Source,f.Brief()));
        else await Assert.ThrowsAnyAsync<IOException>(()=>f.Studio.GenerateAsync(f.Source,f.Brief()));
        Assert.Equal(draft.Id,(await f.Studio.CachedAsync(f.Source))!.Id);Assert.Null((await f.Documents.LoadAsync(f.Source.DocumentId))!.Project!.CoverImageUrl);
    }
    [Fact] public async Task ServerRejectsCrossOwnerDeletedStaleAndPostGenerationChangesBeforeReturningConcepts() {
        using var f=new CoverTestFixture();await f.Start();await using var db=f.Db();var prompt=f.Checked();
        Assert.IsType<ConflictObjectResult>((await f.Controller(db,"other").Generate(prompt,default)).Result);Assert.Equal(0,f.Provider.Calls);
        var record=await db.DocumentSyncRecords.SingleAsync();record.Version="new-version";await db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>((await f.Controller(db).Generate(prompt,default)).Result);Assert.Equal(0,f.Provider.Calls);
        record.Version="cover-v1";await db.SaveChangesAsync();
        f.Provider.During=async _=>{await using var changed=f.Db();var project=await changed.Projects.SingleAsync();project.Title="changed";await changed.SaveChangesAsync();};
        Assert.IsType<ConflictObjectResult>((await f.Controller(db).Generate(prompt,default)).Result);Assert.Equal(1,f.Provider.Calls);
    }
    [Fact] public async Task AtomicCoverFailureAndCancellationKeepOldCoverAndRecoveryTogether() {
        using var f=new CoverTestFixture();await f.Start();var saved=await f.Documents.SetProjectCoverAsync(f.Source,CoverTestFixture.Image,Guid.NewGuid());
        var store=new FileLocalDocumentStore(f.Root+"/docs",TimeProvider.System,new AtomicDocumentWriter(_=>throw new IOException("disk full")));
        await Assert.ThrowsAsync<IOException>(()=>store.SetProjectCoverAsync(saved,CoverTestFixture.Image,Guid.NewGuid()));
        var after=(await f.Documents.LoadAsync(saved.DocumentId))!;Assert.Equal(saved.Project!.CoverChangeId,after.Project!.CoverChangeId);Assert.Equal(saved.Project.PreviousCoverImageUrl,after.Project.PreviousCoverImageUrl);
        using var cancellation=new CancellationTokenSource();store=new FileLocalDocumentStore(f.Root+"/docs",TimeProvider.System,new AtomicDocumentWriter(_=>cancellation.Cancel()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>store.SetProjectCoverAsync(after,CoverTestFixture.Image,Guid.NewGuid(),ct:cancellation.Token));
        Assert.Equal(after.Project.CoverChangeId,(await f.Documents.LoadAsync(after.DocumentId))!.Project!.CoverChangeId);
    }
    [Fact] public async Task RecoveryCanRestoreNoCoverAndSiblingDocumentsShareTheSavedAsset() {
        using var f=new CoverTestFixture();await f.Start();var sibling=await f.Documents.CreateProjectDocumentAsync(f.Source.Project!.ProjectId,"Notes","notes");
        var saved=await f.Documents.SetProjectCoverAsync(f.Source,CoverTestFixture.Image,Guid.NewGuid());Assert.Equal(saved.Project!.CoverImageUrl,(await f.Documents.LoadAsync(sibling.DocumentId))!.Project!.CoverImageUrl);
        await Assert.ThrowsAsync<LocalDocumentConflictException>(()=>f.Documents.SetProjectCoverAsync(f.Source,CoverTestFixture.Image,Guid.NewGuid()));
        var restored=await f.Documents.SetProjectCoverAsync(saved,null,Guid.NewGuid(),restore:true);Assert.Null(restored.Project!.CoverImageUrl);Assert.Equal(CoverTestFixture.Image,restored.Project.PreviousCoverImageUrl);
    }
    [Fact] public async Task AccountSwitchAtAtomicSaveCancelsPromotionAndPreservesPreviousProjectCover() {
        using var f=new CoverTestFixture();await f.Start();var draft=await f.Studio.GenerateAsync(f.Source,f.Brief());
        var faulting=new FileLocalDocumentStore(f.Root+"/docs",TimeProvider.System,new AtomicDocumentWriter(_=>f.Account.SignOutAsync().GetAwaiter().GetResult()));
        var studio=new DeviceCoverStudio(f.Http,f.Account,f.Network,f.Host,new LocalDocumentRepository(faulting),f.Cache);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>studio.SaveAsync(f.Source,draft));
        var current=(await f.Documents.LoadAsync(f.Source.DocumentId))!;Assert.Null(current.Project!.CoverImageUrl);Assert.False(current.Project.HasCoverRecovery);
    }
    [Fact] public async Task DeletedConflictAndMovedProjectDraftsCannotChangeCovers() {
        using var f=new CoverTestFixture();await f.Start();var draft=await f.Studio.GenerateAsync(f.Source,f.Brief());
        Assert.Null(await f.Studio.CachedAsync(f.Source with{Project=f.Source.Project! with{ProjectId=Guid.NewGuid()}}));
        var trashed=await f.Documents.MoveToTrashAsync(f.Source);await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Studio.SaveAsync(trashed,draft));
        Assert.Null((await f.Documents.LoadAsync(trashed.DocumentId))!.Project!.CoverImageUrl);
    }
    [Fact] public void PngValidationRejectsChecksumTrailingDataOversizeAndUnsupportedMime() {
        byte[] valid=Convert.FromBase64String(CoverTestFixture.Png);CoverStudioContract.ValidatePng(valid);
        byte[] invalid=valid.ToArray();invalid[45]^=1;Assert.Throws<InvalidDataException>(()=>CoverStudioContract.ValidatePng(invalid));
        Assert.Throws<InvalidDataException>(()=>CoverStudioContract.ValidatePng(valid.Concat(new byte[]{0}).ToArray()));
        Assert.Throws<InvalidDataException>(()=>CoverStudioContract.ValidatePng(new byte[CoverStudioContract.MaxBytes+1]));
    }
    [Fact] public void PngWithValidChunkChecksumButInvalidCompressedPixelsIsRejected() {
        byte[] png=Convert.FromBase64String(CoverTestFixture.SecondPng);int offset=33;
        int size=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset,4));png[offset+8]=0;
        uint crc=0xffffffff;foreach(byte value in png.AsSpan(offset+4,size+4)){crc^=value;for(int bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)==1?0xedb88320u:0u);}
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(offset+8+size,4),crc^0xffffffff);
        Assert.Throws<InvalidDataException>(()=>CoverStudioContract.ValidatePng(png));
    }
}
