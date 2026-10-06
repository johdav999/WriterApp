using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DocumentSyncTests
{
    [Fact] public async Task CoverEnvelopeAllowsBoundedAssetButKeepsOriginalWritingLimit() {
        Guid id=Guid.NewGuid();var request=ProjectRequest();byte[] png=Convert.FromBase64String(CoverTestFixture.Png);
        byte[] text=new byte[1600*1024];System.Text.Encoding.ASCII.GetBytes("Pad\0").CopyTo(text,0);Array.Fill(text,(byte)'x',4,text.Length-4);
        byte[] chunk=new byte[text.Length+12];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(0,4),text.Length);"tEXt"u8.CopyTo(chunk.AsSpan(4));text.CopyTo(chunk,8);
        uint crc=0xffffffff;foreach(byte value in chunk.AsSpan(4,text.Length+4)){crc^=value;for(int bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)==1?0xedb88320u:0u);}
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(text.Length+8,4),crc^0xffffffff);
        byte[] padded=png[..33].Concat(chunk).Concat(png[33..]).ToArray();
        request=request with{Document=request.Document! with{Project=request.Document.Project! with{CoverImageUrl="data:image/png;base64,"+Convert.ToBase64String(padded)}}};
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(request).Length>2*1024*1024);
        await _sync.MutateAsync("paid",id,request,projects:true,planning:true);Assert.NotNull((await _db.Projects.SingleAsync()).CoverImageUrl);
        var tooMuchWriting=ProjectRequest();var section=tooMuchWriting.Document!.Sections[0];var page=section.Pages[0];
        tooMuchWriting=tooMuchWriting with{Document=tooMuchWriting.Document with{Sections=[section with{Pages=Enumerable.Range(0,6).Select(i=>page with{Id=Guid.NewGuid(),OrderIndex=i,Content="<p>"+new string('x',400_000)+"</p>"}).ToArray()}]}};
        var denied=await Assert.ThrowsAsync<DocumentSyncException>(()=>_sync.MutateAsync("paid",Guid.NewGuid(),tooMuchWriting,projects:true,planning:true));Assert.Equal(413,denied.Status);Assert.Single(await _db.Documents.ToListAsync());
    }
    [Fact] public async Task SelectedCoverRoundTripsThroughRealProjectSyncWithConflictProtection() {
        Guid id=Guid.NewGuid();var initial=ProjectRequest();
        initial=initial with{Document=initial.Document! with{Project=initial.Document.Project! with{Version=3,PrimaryDocumentId=id}}};
        var created=await _sync.MutateAsync("paid",id,initial,projects:true,planning:true,multipleDocuments:true);
        var snapshot=await _sync.DownloadAsync("paid",id,projects:true,planning:true,multipleDocuments:true);
        var local=DeviceSyncMapping.Download(snapshot,Guid.NewGuid(),null,DateTimeOffset.UtcNow);
        local=local with{Project=local.Project! with{CoverImageUrl=CoverTestFixture.Image}};
        var upload=DeviceSyncMapping.Upload(local);await _sync.MutateAsync("paid",id,new(Guid.NewGuid(),created.State.Version,"upload",upload),projects:true,planning:true,multipleDocuments:true);
        var after=await _sync.DownloadAsync("paid",id,projects:true,planning:true,multipleDocuments:true);
        Assert.Equal(CoverTestFixture.Image,after.Document!.Project!.CoverImageUrl);Assert.Equal(JsonSerializer.Serialize(snapshot.Document!.Sections),JsonSerializer.Serialize(after.Document.Sections));
        Assert.Equal(409,(await Assert.ThrowsAsync<DocumentSyncException>(()=>_sync.MutateAsync("paid",id,new(Guid.NewGuid(),created.State.Version,"upload",initial.Document),projects:true,planning:true,multipleDocuments:true))).Status);
        Assert.Equal(CoverTestFixture.Image,(await _db.Projects.SingleAsync()).CoverImageUrl);
        Assert.Equal(404,(await Assert.ThrowsAsync<DocumentSyncException>(()=>_sync.MutateAsync("other-paid",id,new(Guid.NewGuid(),after.State.Version,"upload",upload),projects:true,planning:true,multipleDocuments:true))).Status);
    }
    [Theory][InlineData("data:image/svg+xml;base64,PHN2Zz4=")][InlineData("data:image/png;base64,aGVsbG8=")]
    public async Task InvalidInlineCoverSyncDoesNotPersistAnyProjectOrWriting(string cover) {
        Guid id=Guid.NewGuid();var request=ProjectRequest();request=request with{Document=request.Document! with{Project=request.Document.Project! with{CoverImageUrl=cover}}};
        Assert.Equal(400,(await Assert.ThrowsAsync<DocumentSyncException>(()=>_sync.MutateAsync("paid",id,request,projects:true,planning:true))).Status);
        Assert.Empty(await _db.Documents.ToListAsync());Assert.Empty(await _db.Projects.ToListAsync());
    }
}
