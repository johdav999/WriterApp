using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WriterApp.Shared.Brainstorming;

public sealed class BrainstormingSpeechHttp(HttpClient http,Func<BrainstormingScope,BrainstormingRemoteLease> lease,
    Func<BrainstormingScope,CancellationToken,Task<BrainstormingScope>> mapped):IBrainstormingSpeechTranscriber {
    private const string Route="api/brainstorming/dictation";
    public async Task PrepareAsync(BrainstormingScope local,CancellationToken ct=default) {var server=await mapped(local,ct);lease(server).RequireCurrent();}
    public async Task<BrainstormingSpeechResult> TranscribeAsync(BrainstormingScope local,Guid operation,string language,byte[] wav,CancellationToken ct=default) {
        var server=await mapped(local,ct);var value=new BrainstormingSpeechRequest(1,server,operation,language,wav);BrainstormingSpeech.Validate(value);
        using var request=new HttpRequestMessage(HttpMethod.Post,Route){Content=JsonContent.Create(value,options:BrainstormingValidation.Json)};
        return await Send(server,operation,request,false,ct)??throw new InvalidDataException("Missing dictation response.");
    }
    public async Task<BrainstormingSpeechResult?> InspectAsync(BrainstormingScope local,Guid operation,CancellationToken ct=default) {
        var server=await mapped(local,ct);using var request=new HttpRequestMessage(HttpMethod.Get,$"{Route}/{server.SessionId}/{operation}?documentId={server.DocumentId}&projectId={server.ProjectId}");return await Send(server,operation,request,true,ct);
    }
    private async Task<BrainstormingSpeechResult?> Send(BrainstormingScope server,Guid operation,HttpRequestMessage request,bool allowMissing,CancellationToken ct) {
        var guard=lease(server);guard.RequireCurrent();guard.Configure?.Invoke(request);using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(100));
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);guard.RequireCurrent();
        await using var input=await response.Content.ReadAsStreamAsync(timeout.Token);using var bytes=new MemoryStream();var block=new byte[4096];int count;
        while((count=await input.ReadAsync(block,timeout.Token))>0){if(bytes.Length+count>80_000)throw new InvalidDataException("Oversized transcript; composer retained.");bytes.Write(block,0,count);}
        guard.RequireCurrent();ct.ThrowIfCancellationRequested();
        if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || response.StatusCode==HttpStatusCode.NotFound && System.Text.Encoding.UTF8.GetString(bytes.ToArray()).Contains("access_revoked",StringComparison.Ordinal)){guard.AccessRevoked?.Invoke();throw new BrainstormingSpeechException("auth","Dictation access changed. Reopen the original account.");}
        if(response.StatusCode==HttpStatusCode.NotFound && allowMissing && System.Text.Encoding.UTF8.GetString(bytes.ToArray()).Contains("operation_missing",StringComparison.Ordinal))return null;
        if(!response.IsSuccessStatusCode)throw new BrainstormingSpeechException(response.StatusCode==HttpStatusCode.TooManyRequests?"quota":"provider","Dictation unavailable. Check authorization, quota, connectivity and configured speech provider; your composer is retained. Inspect a dispatched recording before retrying.");
        try {var result=JsonSerializer.Deserialize<BrainstormingSpeechResult>(bytes.ToArray(),BrainstormingValidation.Json)??throw new InvalidDataException("Missing dictation outcome.");BrainstormingSpeech.Validate(result,server,operation);return result;}
        catch(JsonException){throw new InvalidDataException("Malformed dictation outcome; composer retained. Inspect the original operation.");}
    }
}
