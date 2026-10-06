using System;
using System.IO;
using System.Buffers.Binary;
using System.IO.Compression;

namespace WriterApp.Shared;

public static class CoverStudioContract
{
    public static readonly string[] Genres = ["Fantasy", "Romance", "Sci-fi", "Thriller", "Mystery", "Literary"];
    public static readonly string[] Moods = ["Dark", "Hopeful", "Epic", "Mysterious", "Romantic"];
    public static readonly string[] Styles = ["Cinematic", "Painted", "Illustrated", "Minimalist", "Photorealistic"];
    public static readonly string[] Palettes = ["Warm", "Cold", "Dark", "High contrast", "Pastel"];
    public const int MaxBytes = 2 * 1024 * 1024;
    public const int MaxDataUriLength = (MaxBytes + 2) / 3 * 4 + 22;
    public static void ValidatePrompt(CoverPrompt prompt)
    {
        if (prompt.Description?.Length > 4000 || prompt.Genre?.Length > 80 || prompt.Mood?.Length > 80
            || prompt.Style?.Length > 80 || prompt.ColorPalette?.Length > 80 || prompt.ContractVersion is not (0 or 1))
            throw new ArgumentException("Use a cover description of at most 4000 characters and short style choices.");
        if (prompt.ContractVersion == 1 && (prompt.ProjectId is null || prompt.ProjectId == Guid.Empty || prompt.DocumentId is null
            || prompt.DocumentId == Guid.Empty || prompt.ExpectedMetadataRevision is null or < 0
            || string.IsNullOrWhiteSpace(prompt.ExpectedDocumentVersion) || prompt.ExpectedDocumentVersion.Length > 64))
            throw new ArgumentException("Synchronize the project and document before generating a cover.");
    }
    public static byte[] ReadPng(string value)
    {
        const string prefix = "data:image/png;base64,";
        if (value is null || value.Length > MaxDataUriLength || !value.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidDataException("Cover concepts must be inline PNG images of at most 2 MB. Remote URLs and other media are unavailable; choose a local PNG or retry generation.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(value[prefix.Length..]); }
        catch (FormatException e) { throw new InvalidDataException("The PNG concept is invalid.", e); }
        ValidatePng(bytes); return bytes;
    }
    public static void ValidatePng(byte[] bytes)
    {
        if (bytes is null || bytes.Length < 45 || bytes.Length > MaxBytes || !bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})
            || !bytes.AsSpan(12,4).SequenceEqual("IHDR"u8) || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8,4)) != 13)
            throw new InvalidDataException("Choose a PNG cover of at most 2 MB.");
        uint width=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4)), height=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4));
        if(width==0 || height==0 || width>8000 || height>8000 || (long)width*height>32_000_000) throw new InvalidDataException("Cover dimensions exceed 8000 pixels or 32 megapixels.");
        int depth=bytes[24], color=bytes[25], channels=color switch { 0=>1,2=>3,3=>1,4=>2,6=>4,_=>0 };
        bool validDepth=color switch { 0=>depth is 1 or 2 or 4 or 8 or 16,3=>depth is 1 or 2 or 4 or 8,2 or 4 or 6=>depth is 8 or 16,_=>false };
        if(!validDepth || bytes[26]!=0 || bytes[27]!=0 || bytes[28]>1) throw new InvalidDataException("Unsupported PNG encoding.");
        int offset=8; bool image=false, palette=false; using var compressed=new MemoryStream();
        while(offset<=bytes.Length-12) {
            uint size=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset,4));
            if(size>bytes.Length-offset-12) throw new InvalidDataException("Truncated PNG cover.");
            uint crc=0xffffffff; foreach(byte b in bytes.AsSpan(offset+4,(int)size+4)) { crc^=b; for(int bit=0;bit<8;bit++) crc=(crc>>1)^((crc&1)==1?0xedb88320u:0u); }
            if((crc^0xffffffff)!=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset+8+(int)size,4))) throw new InvalidDataException("PNG checksum mismatch.");
            image |= bytes.AsSpan(offset+4,4).SequenceEqual("IDAT"u8) && size>0;
            if(bytes.AsSpan(offset+4,4).SequenceEqual("IDAT"u8)) compressed.Write(bytes.AsSpan(offset+8,(int)size));
            if(bytes.AsSpan(offset+4,4).SequenceEqual("PLTE"u8)) palette=size>0 && size<=768 && size%3==0;
            if(offset!=8 && bytes.AsSpan(offset+4,4).SequenceEqual("IHDR"u8)) throw new InvalidDataException("Duplicate PNG header.");
            if(bytes.AsSpan(offset+4,4).SequenceEqual("IEND"u8)) {
                if(size != 0 || offset + 12 != bytes.Length || !image || (color==3 && !palette)) throw new InvalidDataException("Invalid PNG ending or palette.");
                compressed.Position=0;
                ValidateRaster(compressed,(int)width,(int)height,channels*depth,bytes[28]==1);
                return;
            }
            offset+=checked((int)size+12);
        }
        throw new InvalidDataException("Truncated PNG cover.");
    }
    private static void ValidateRaster(Stream compressed,int width,int height,int bits,bool interlaced)
    {
        try {
            using var decoded=new ZLibStream(compressed,CompressionMode.Decompress,leaveOpen:true); byte[] block=new byte[8192]; long consumed=0;
            var passes=interlaced ? new[]{(0,0,8,8),(4,0,8,8),(0,4,4,8),(2,0,4,4),(0,2,2,4),(1,0,2,2),(0,1,1,2)} : new[]{(0,0,1,1)};
            foreach(var (x,y,dx,dy) in passes) {
                int columns=width<=x?0:(width-x+dx-1)/dx,rows=height<=y?0:(height-y+dy-1)/dy;
                if(columns==0 || rows==0)continue;
                int rowBytes=(columns*bits+7)/8;
                if(consumed+(long)rows*(rowBytes+1)>256*1024*1024) throw new InvalidDataException("PNG expands beyond the safe limit.");
                for(int row=0;row<rows;row++) {
                    int filter=decoded.ReadByte();if(filter is <0 or >4) throw new InvalidDataException("Invalid or truncated PNG pixels.");
                    for(int left=rowBytes;left>0;) { int read=decoded.Read(block,0,Math.Min(left,block.Length));if(read==0)throw new InvalidDataException("Truncated PNG pixels.");left-=read; }
                }
                consumed+=(long)rows*(rowBytes+1);
            }
            if(decoded.ReadByte()!=-1) throw new InvalidDataException("PNG contains unexpected pixel data.");
        }
        catch(IOException e) { throw new InvalidDataException("The PNG pixel data could not be read safely.",e); }
    }
}
