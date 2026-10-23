using System.Buffers.Binary;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Android;

public static class AndroidPcmWav
{
    public static byte[] Encode(ReadOnlySpan<byte> pcm)
    {
        if (pcm.Length is < 3200 or > BrainstormingSpeech.MaxSeconds * 32000 || pcm.Length % 2 != 0)
            throw new InvalidDataException("Record between 0.1 and 60 seconds of speech.");
        byte[] wav = new byte[44 + pcm.Length];
        "RIFF"u8.CopyTo(wav); BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(4), wav.Length - 8);
        "WAVEfmt "u8.CopyTo(wav.AsSpan(8)); BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(20), 1); BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(22), 1);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(24), 16000); BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(28), 32000);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(32), 2); BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(34), 16);
        "data"u8.CopyTo(wav.AsSpan(36)); BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(40), pcm.Length);
        pcm.CopyTo(wav.AsSpan(44));
        BrainstormingSpeech.ValidateWav(wav);
        return wav;
    }
}
