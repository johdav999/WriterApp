using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using WriterApp.Application.Covers;

namespace WriterApp.Tests;

internal sealed class RemoteCoverNetwork : ICoverAssetNetwork
{
    public const string Reference="https://provider.test/owned-container/cover.png?signature=synthetic";
    public int Calls;public readonly List<Uri> Targets=[];
    public IPAddress[] Addresses=[IPAddress.Parse("93.184.216.34")];
    public string Scenario="png";public bool Second;public Func<Task>? During;
    public TrustedCoverFetcher Fetcher()=>new(this,Options.Create(new CoverAssetOptions {TrustedStoragePrefixes=["https://provider.test/owned-container/"]}));
    public Task<IPAddress[]> ResolveAsync(string host,CancellationToken ct)=>Task.FromResult(Addresses);
    public async Task<CoverDownload> GetAsync(Uri uri,IPAddress[] addresses,CancellationToken ct) {
        Calls++;Targets.Add(uri);if(During is not null)await During();ct.ThrowIfCancellationRequested();
        if(Scenario.StartsWith("redirect") && (Calls==1 || Scenario=="redirect-loop")) {
            var response=new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location=new(Scenario switch {"redirect-foreign"=>"https://foreign.test/owned-container/cover.png",
                "redirect-private"=>"https://127.0.0.1/owned-container/cover.png","redirect-path"=>"https://provider.test/other/cover.png",
                _=>"https://provider.test/owned-container/redirected.png"});return new(response);
        }
        if(Scenario is "expired" or "deleted")return new(new(Scenario=="expired" ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound));
        var bytes=Convert.FromBase64String(Second?CoverTestFixture.SecondPng:CoverTestFixture.Png);
        if(Scenario=="corrupt")bytes[20]^=1;
        if(Scenario=="oversize-stream")bytes=new byte[WriterApp.Shared.CoverStudioContract.MaxBytes+1];
        if(Scenario=="signature")bytes=[255,216,255,224,0,1,2,3];
        HttpContent content=Scenario=="partial" ? new PartialContent(bytes) : new ByteArrayContent(bytes);
        content.Headers.ContentType=new MediaTypeHeaderValue(Scenario=="jpeg" ? "image/jpeg" : "image/png");
        if(Scenario=="encoding")content.Headers.ContentEncoding.Add("gzip");
        if(Scenario=="oversize-header")content.Headers.ContentLength=WriterApp.Shared.CoverStudioContract.MaxBytes+1;
        if(Scenario=="length")content.Headers.ContentLength=bytes.Length+1;
        if(Scenario=="oversize-stream")content.Headers.ContentLength=null;
        return new(new(HttpStatusCode.OK){Content=content});
    }
    private sealed class PartialContent(byte[] bytes):HttpContent {
        protected override bool TryComputeLength(out long length){length=0;return false;}
        protected override Task SerializeToStreamAsync(Stream stream,TransportContext? context)=>throw new IOException("Partial download.");
        protected override Task<Stream> CreateContentReadStreamAsync()=>Task.FromResult<Stream>(new PartialStream(bytes));
    }
    private sealed class PartialStream(byte[] bytes):MemoryStream(bytes) {
        private bool read;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken ct=default){if(read)throw new IOException("Partial download.");read=true;return base.ReadAsync(buffer[..Math.Min(buffer.Length,12)],ct);}
    }
}
