using System.Buffers.Binary;

namespace WriterApp.Shared.Brainstorming;

public enum BrainstormingSpeechState { Transcribing, Completed, Silence, Failed, Cancelled, Unknown }
public enum BrainstormingRecognitionQuality { Unassessed, NeedsReview, Clear }
public sealed record BrainstormingSpeechRequest(int Version,BrainstormingScope Scope,Guid OperationId,string Language,byte[] Wav);
public sealed record BrainstormingSpeechResult(int Version,BrainstormingScope Scope,Guid OperationId,string Language,
    BrainstormingSpeechState State,string Text,string Message,BrainstormingGenerationUsage Usage,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] BrainstormingRecognitionQuality Quality=BrainstormingRecognitionQuality.Unassessed);
public sealed record BrainstormingDictationJournal(Guid OperationId,string Language,BrainstormingSpeechResult? Result,bool Adopted=false);
public sealed class BrainstormingSpeechException(string code,string message):IOException(message) { public string Code {get;}=code; }
public interface IBrainstormingRecording:IAsyncDisposable {
    Task<byte[]> Audio {get;}
    Task StopAsync();
    Task CancelAsync();
}
public interface IBrainstormingMicrophone {
    Task<BrainstormingCapability> AvailabilityAsync(CancellationToken ct=default);
    Task<IBrainstormingRecording> StartAsync(CancellationToken ct=default);
    Task CancelAsync();
}
public sealed class UnsupportedBrainstormingMicrophone:IBrainstormingMicrophone {
    public Task<BrainstormingCapability> AvailabilityAsync(CancellationToken ct=default)=>Task.FromResult(new BrainstormingCapability(BrainstormingCapabilityState.UnsupportedPlatform,"Microphone capture is unavailable on this platform. Type your message."));
    public Task<IBrainstormingRecording> StartAsync(CancellationToken ct=default)=>throw new BrainstormingSpeechException("unsupported","Microphone capture is unavailable on this platform.");
    public Task CancelAsync()=>Task.CompletedTask;
}
public interface IBrainstormingSpeechTranscriber {
    Task PrepareAsync(BrainstormingScope local,CancellationToken ct=default);
    Task<BrainstormingSpeechResult> TranscribeAsync(BrainstormingScope local,Guid operation,string language,byte[] wav,CancellationToken ct=default);
    Task<BrainstormingSpeechResult?> InspectAsync(BrainstormingScope local,Guid operation,CancellationToken ct=default);
}
public static class BrainstormingSpeech {
    public const int MaxSeconds=60,MaxAudioBytes=1_921_024,MaxTranscript=8_000;
    public static void RequireLanguage(string language) {if(language is not ("en" or "sv"))throw new InvalidDataException("Choose English or Swedish for dictation.");}
    public static (double Seconds,bool Silence) ValidateWav(byte[] wav) {
        if(wav is null || wav.Length<44 || wav.Length>MaxAudioBytes || !wav.AsSpan(0,4).SequenceEqual("RIFF"u8) || !wav.AsSpan(8,4).SequenceEqual("WAVE"u8)
            || BinaryPrimitives.ReadUInt32LittleEndian(wav.AsSpan(4,4))!=wav.Length-8)throw new InvalidDataException("Invalid or oversized WAV recording.");
        bool format=false;int data=-1,length=0;
        for(int offset=12;offset+8<=wav.Length;) {
            uint size=BinaryPrimitives.ReadUInt32LittleEndian(wav.AsSpan(offset+4,4));if(size>int.MaxValue || (long)offset+8+size>wav.Length)throw new InvalidDataException("Truncated audio recording.");
            if(wav.AsSpan(offset,4).SequenceEqual("fmt "u8)) {
                if(format || size<16 || BinaryPrimitives.ReadUInt16LittleEndian(wav.AsSpan(offset+8,2))!=1 || BinaryPrimitives.ReadUInt16LittleEndian(wav.AsSpan(offset+10,2))!=1
                    || BinaryPrimitives.ReadUInt32LittleEndian(wav.AsSpan(offset+12,4))!=16000 || BinaryPrimitives.ReadUInt32LittleEndian(wav.AsSpan(offset+16,4))!=32000
                    || BinaryPrimitives.ReadUInt16LittleEndian(wav.AsSpan(offset+20,2))!=2 || BinaryPrimitives.ReadUInt16LittleEndian(wav.AsSpan(offset+22,2))!=16)throw new InvalidDataException("Dictation requires mono 16 kHz PCM16 WAV.");format=true;
            }
            if(wav.AsSpan(offset,4).SequenceEqual("data"u8)){if(data!=-1)throw new InvalidDataException("Multiple audio payloads are unsupported.");data=offset+8;length=(int)size;}
            offset=checked(offset+8+(int)size+((int)size%2));
        }
        if(!format || data<0 || length<3200 || length%2!=0 || length>MaxSeconds*32000)throw new InvalidDataException("Record between 0.1 and 60 seconds of speech.");
        double energy=0;for(int i=data;i<data+length;i+=2){var sample=BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(i,2));energy+=(double)sample*sample;}
        return (length/32000d,Math.Sqrt(energy/(length/2))<8);
    }
    public static void Validate(BrainstormingSpeechRequest r) {if(r.Version!=1 || r.OperationId==Guid.Empty)throw new InvalidDataException("Invalid dictation request.");BrainstormingValidation.Validate(r.Scope);RequireLanguage(r.Language);ValidateWav(r.Wav);}
    public static void Validate(BrainstormingSpeechResult r,BrainstormingScope server,Guid operation) {
        if(r.Version!=1 || r.OperationId!=operation || operation==Guid.Empty || !Enum.IsDefined(r.State) || !Enum.IsDefined(r.Quality) || r.Text is null || r.Text.Length>MaxTranscript || r.Text.Contains('\0')
            || r.Message is null || r.Message.Length>2000 || r.Usage is null || r.Usage.InputTokens<0 || r.Usage.OutputTokens<0 || !Enum.IsDefined(r.Usage.State)
            || r.State==BrainstormingSpeechState.Completed && string.IsNullOrWhiteSpace(r.Text) || r.State!=BrainstormingSpeechState.Completed && r.Text.Length!=0)throw new InvalidDataException("Invalid dictation response; composer retained.");
        BrainstormingValidation.RequireScope(r.Scope,server);RequireLanguage(r.Language);
    }
}
