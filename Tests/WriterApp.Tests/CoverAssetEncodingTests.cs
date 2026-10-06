using System.Buffers.Binary;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class CoverAssetEncodingTests
{
    private static void Crc(byte[] png,int offset) {
        int size=BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset,4));uint crc=0xffffffff;
        foreach(byte b in png.AsSpan(offset+4,size+4)){crc^=b;for(int bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)==1?0xedb88320u:0u);}
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(offset+8+size,4),crc^0xffffffff);
    }
    [Theory][InlineData("dimensions")][InlineData("raster")][InlineData("animated")][InlineData("critical")]
    public void RemoteEncodingAndExpansionRefuseBeforeCachingEvenWithValidChunkChecksums(string scenario) {
        var png=Convert.FromBase64String(CoverTestFixture.Png);
        if(scenario is "dimensions" or "raster") {
            BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16,4),scenario=="dimensions"?8001u:8000u);
            BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20,4),scenario=="dimensions"?8001u:4000u);Crc(png,8);
        } else {
            var chunk=new byte[12];System.Text.Encoding.ASCII.GetBytes(scenario=="animated" ? "acTL":"TEST").CopyTo(chunk,4);Crc(chunk,0);
            png=png[..33].Concat(chunk).Concat(png[33..]).ToArray();
        }
        Assert.Throws<InvalidDataException>(()=>CoverAssetContract.ValidateRemotePng(png));
    }
}
