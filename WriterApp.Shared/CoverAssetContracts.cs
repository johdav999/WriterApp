using System.Security.Cryptography;
using System.Text;

namespace WriterApp.Shared;

public sealed record CoverAssetSource(int Version, Guid ProjectId, Guid DocumentId, long MetadataRevision,
    string DocumentVersion, string ReferenceHash);
public sealed record CoverAssetIdentity(int Version, Guid AssetId, Guid ProjectId, Guid SourceDocumentId,
    long SourceMetadataRevision, string SourceDocumentVersion, string RemoteReference, string ContentHash,
    string MediaType, int ByteLength);
public sealed record CoverAssetResponse(CoverAssetSource Source, CoverAssetIdentity Asset, byte[] Bytes);

public static class CoverAssetContract
{
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static string ReferenceHash(string reference) => Hash(Encoding.UTF8.GetBytes(reference));
    public static void ValidateSource(CoverAssetSource source) {
        if(source is null || source.Version!=1 || source.ProjectId==Guid.Empty || source.DocumentId==Guid.Empty || source.MetadataRevision<0
            || source.DocumentVersion is not {Length:>0 and <=64} || source.ReferenceHash is not {Length:64}
            || !source.ReferenceHash.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid owned cover source. Synchronize and reopen the cover studio.");
    }
    public static void Validate(CoverAssetIdentity asset, byte[] bytes, Guid project) {
        if(asset is null || bytes is null)throw new InvalidDataException("Missing owned cover identity or bytes. Existing covers are preserved.");
        ValidateRemotePng(bytes);
        bool provenance = asset.Version==1 && Uri.TryCreate(asset.RemoteReference,UriKind.Absolute,out var uri)
            && uri.Scheme==Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
            || asset.Version==2 && asset.RemoteReference=="urn:writerapp:cover:"+asset.ContentHash;
        if(!provenance || asset.AssetId==Guid.Empty || asset.ProjectId!=project || asset.SourceDocumentId==Guid.Empty
            || asset.SourceMetadataRevision<0 || asset.SourceDocumentVersion is not {Length:>0 and <=64}
            || asset.RemoteReference is not {Length:>0 and <=4096} || asset.MediaType!="image/png"
            || asset.ByteLength!=bytes.Length || asset.ContentHash!=Hash(bytes))
            throw new InvalidDataException("The backend did not return the exact validated owned PNG asset.");
    }
    public static void ValidateRemotePng(byte[] bytes) {
        CoverStudioContract.ValidatePng(bytes);
        for(int offset=8;offset<=bytes.Length-12;) {
            var kind=bytes.AsSpan(offset+4,4);
            if(kind.SequenceEqual("acTL"u8) || kind.SequenceEqual("fcTL"u8) || kind.SequenceEqual("fdAT"u8)
                || (bytes[offset+4]&32)==0 && !kind.SequenceEqual("IHDR"u8) && !kind.SequenceEqual("PLTE"u8) && !kind.SequenceEqual("IDAT"u8) && !kind.SequenceEqual("IEND"u8))
                throw new InvalidDataException("Animated PNG and unknown critical PNG encodings are unsupported. Keep the prior cover or choose a static PNG.");
            offset+=checked((int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset,4))+12);
        }
    }
}
