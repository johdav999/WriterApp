using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using WriterApp.Shared;

namespace WriterApp.Application.Covers;

public sealed class CoverAssetOptions
{
    // Exact trusted provider account/container paths, ending in '/'. Empty disables remote fetch.
    public string[] TrustedStoragePrefixes { get; set; } = [];
}
public sealed class CoverAssetException(int status,string code,string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public sealed record CoverDownload(HttpResponseMessage Response, IDisposable? Connection = null) : IDisposable
{
    public void Dispose() { Response.Dispose();Connection?.Dispose(); }
}
public interface ICoverAssetNetwork
{
    Task<IPAddress[]> ResolveAsync(string host,CancellationToken ct);
    Task<CoverDownload> GetAsync(Uri uri,IPAddress[] addresses,CancellationToken ct);
}
public sealed class CoverAssetNetwork : ICoverAssetNetwork
{
    public Task<IPAddress[]> ResolveAsync(string host,CancellationToken ct) => Dns.GetHostAddressesAsync(host,ct);
    public async Task<CoverDownload> GetAsync(Uri uri,IPAddress[] addresses,CancellationToken ct) {
        var handler=new SocketsHttpHandler {AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.None,UseCookies=false,UseProxy=false,
            ConnectTimeout=TimeSpan.FromSeconds(10),MaxResponseHeadersLength=16};
        handler.ConnectCallback=async (context,token) => {
            if(context.DnsEndPoint.Host!=uri.IdnHost || context.DnsEndPoint.Port!=443)throw new IOException("Cover connection target changed.");
            foreach(var address in addresses) {
                if(!TrustedCoverFetcher.IsPublic(address))throw new IOException("Cover address is private.");
                var socket=new Socket(address.AddressFamily,SocketType.Stream,ProtocolType.Tcp);
                try { await socket.ConnectAsync(new IPEndPoint(address,443),token);return new NetworkStream(socket,ownsSocket:true); }
                catch(SocketException) { socket.Dispose(); }
                catch { socket.Dispose();throw; }
            }
            throw new IOException("Trusted cover storage is unavailable.");
        };
        var client=new HttpClient(handler){Timeout=Timeout.InfiniteTimeSpan};
        try { var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct);return new(response,client); }
        catch { client.Dispose();throw; }
    }
}
public sealed class TrustedCoverFetcher(ICoverAssetNetwork network,IOptions<CoverAssetOptions> options)
{
    private static CoverAssetException Unsupported(string message) => new(422,"cover.asset_unsupported",message);
    public static bool IsPublic(IPAddress address) {
        if(address.IsIPv4MappedToIPv6)address=address.MapToIPv4();
        var b=address.GetAddressBytes();
        if(b.Length==16) return (b[0]&0xe0)==0x20 && !(b[0]==0x20 && b[1]==1 && (b[2]==0 || b[2]==0x0d && b[3]==0xb8))
            && !(b[0]==0x20 && b[1]==2); // Refuse local/reserved and tunnelling ranges.
        return b[0] is not (0 or 10 or 127) && b[0]<224 && !(b[0]==100 && b[1] is >=64 and <=127)
            && !(b[0]==169 && b[1]==254) && !(b[0]==172 && b[1] is >=16 and <=31)
            && !(b[0]==192 && (b[1]==168 || b[1]==0 || b[1]==88 && b[2]==99))
            && !(b[0]==198 && (b[1] is 18 or 19 || b[1]==51 && b[2]==100)) && !(b[0]==203 && b[1]==0 && b[2]==113);
    }
    public Uri ValidateReference(string reference) {
        if(reference.Length>4096 || !Uri.TryCreate(reference,UriKind.Absolute,out var uri) || uri.Scheme!="https" || uri.Port!=443
            || uri.UserInfo.Length>0 || uri.Fragment.Length>0 || uri.HostNameType!=UriHostNameType.Dns || !uri.IdnHost.Contains('.')
            || IPAddress.TryParse(uri.IdnHost,out _) || uri.AbsolutePath.Contains('%') || uri.AbsolutePath.Contains('\\'))
            throw Unsupported("Remote covers must use configured trusted HTTPS provider storage. Choose a local PNG or regenerate.");
        bool allowed=options.Value.TrustedStoragePrefixes.Any(prefix => Uri.TryCreate(prefix,UriKind.Absolute,out var root)
            && root.Scheme=="https" && root.Port==443 && root.UserInfo.Length==0 && root.Query.Length==0 && root.Fragment.Length==0
            && root.AbsolutePath.EndsWith('/') && root.AbsolutePath.Length>1 && root.IdnHost==uri.IdnHost
            && uri.AbsolutePath.StartsWith(root.AbsolutePath,StringComparison.Ordinal));
        if(!allowed)throw Unsupported("This cover is outside the configured provider account/container. Keep the saved cover or choose a PNG.");
        return uri;
    }
    public async Task<byte[]> FetchAsync(string reference,CancellationToken ct) {
        var uri=ValidateReference(reference);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(30));ct=timeout.Token;
        for(int redirects=0;redirects<=2;redirects++) {
            var addresses=await network.ResolveAsync(uri.IdnHost,ct);
            if(addresses.Length is 0 or >16 || addresses.Any(address=>!IsPublic(address)))throw Unsupported("Trusted cover storage resolved to a private, reserved or invalid address set.");
            using var download=await network.GetAsync(uri,addresses,ct);var response=download.Response;
            if(response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect) {
                if(redirects==2 || response.Headers.Location is null)throw Unsupported("Cover storage returned too many or invalid redirects.");
                uri=ValidateReference(new Uri(uri,response.Headers.Location).AbsoluteUri);continue;
            }
            if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Gone)
                throw new CoverAssetException(410,"cover.asset_expired","The owned cover reference expired or was deleted. Existing cover/cache is preserved; regenerate or choose a local PNG.");
            if(!response.IsSuccessStatusCode)throw new CoverAssetException(503,"cover.storage_unavailable","Trusted cover storage is unavailable. Retry materialization; the existing cover/cache is preserved.");
            if(response.Content.Headers.ContentType?.MediaType!="image/png" || response.Content.Headers.ContentEncoding.Count!=0)
                throw Unsupported("Only unencoded PNG responses are supported. JPEG, WebP, GIF and SVG are not converted; keep the prior cover or choose PNG.");
            if(response.Content.Headers.ContentLength is >CoverStudioContract.MaxBytes or 0)throw Unsupported("Remote PNG exceeds 2 MB or is empty.");
            await using var stream=await response.Content.ReadAsStreamAsync(ct);using var buffer=new MemoryStream();byte[] block=new byte[16384];int read;
            while((read=await stream.ReadAsync(block,ct))>0) { if(buffer.Length+read>CoverStudioContract.MaxBytes)throw Unsupported("Remote PNG exceeds 2 MB.");await buffer.WriteAsync(block.AsMemory(0,read),ct); }
            var bytes=buffer.ToArray();
            if(response.Content.Headers.ContentLength is { } length && bytes.Length!=length)throw Unsupported("The remote PNG download was incomplete.");
            try { CoverAssetContract.ValidateRemotePng(bytes); }catch(InvalidDataException e){throw Unsupported(e.Message);}
            return bytes;
        }
        throw Unsupported("Invalid cover redirect.");
    }
}
