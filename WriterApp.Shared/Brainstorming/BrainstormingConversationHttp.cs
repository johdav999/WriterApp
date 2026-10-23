using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace WriterApp.Shared.Brainstorming;

public sealed class BrainstormingConversationHttp(HttpClient http,Func<BrainstormingScope,BrainstormingRemoteLease> lease) : IBrainstormingConversation
{
    private const string Route="api/brainstorming/conversation";
    private static async Task<JsonElement?> Error(HttpResponseMessage response,CancellationToken ct) {
        if(response.Content.Headers.ContentLength>8000)return null;
        await using var stream=await response.Content.ReadAsStreamAsync(ct);using var bytes=new MemoryStream();var block=new byte[1024];int n;
        while((n=await stream.ReadAsync(block,ct))>0) {if(bytes.Length+n>8000)return null;bytes.Write(block,0,n);}
        try {using var json=JsonDocument.Parse(bytes.ToArray());return json.RootElement.Clone();}catch(JsonException){return null;}
    }
    private static async Task Success(HttpResponseMessage response,CancellationToken ct,Action? accessRevoked=null,Guid? sendOperation=null) {
        if(response.IsSuccessStatusCode)return;
        if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) {
            accessRevoked?.Invoke();
            throw new OperationCanceledException("Sign in and reopen the original account's conversation. Local writing is retained.");
        }
        var message=response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden?"Sign in to the original account before using this conversation.":
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented?"Conversation unavailable. Check manuscript access or update the backend.":"Conversation failed. Inspect the retained operation before an explicit retry.";
        if(await Error(response,ct) is { } json) {
            if(response.StatusCode==HttpStatusCode.NotFound && json.TryGetProperty("code",out var code) && code.GetString()=="brainstorming.access_revoked") {
                accessRevoked?.Invoke();
                message="Conversation access was revoked. Reopen the original account; local writing is retained.";
            }
            else if(json.TryGetProperty("message",out var text)&&text.GetString() is {Length:>0 and <2000} detail)message=detail;
            if(sendOperation is { } operation && response.StatusCode==HttpStatusCode.BadRequest
                && json.TryGetProperty("accepted",out var accepted) && accepted.ValueKind==JsonValueKind.False
                && json.TryGetProperty("operationId",out var id) && id.ValueKind==JsonValueKind.String && id.TryGetGuid(out var rejected) && rejected==operation
                && json.TryGetProperty("code",out var rejectionCode) && rejectionCode.ValueKind==JsonValueKind.String
                && rejectionCode.GetString() is {Length:>0 and <=200} reason)
                throw new BrainstormingSendRejectedException(message,reason,operation,response.StatusCode);
        }
        throw new HttpRequestException(message,null,response.StatusCode);
    }
    public async Task<BrainstormingStorySnapshot> ReadStoryAsync(BrainstormingScope scope,CancellationToken ct=default) {
        using var request=new HttpRequestMessage(HttpMethod.Get,$"api/brainstorming/sources/{scope.DocumentId}");
        var value=(await Json<BrainstormingStorySnapshot>(scope,request,false,ct))!;BrainstormingStorySources.Validate(value);
        if(value.Outline.DocumentId!=scope.DocumentId)throw new InvalidDataException("Wrong story source.");return value;
    }
    private async Task<T?> Json<T>(BrainstormingScope scope,HttpRequestMessage request,bool missing,CancellationToken ct) {
        var captured=lease(scope);captured.RequireCurrent();captured.Configure?.Invoke(request);
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);captured.RequireCurrent();
        if(missing && response.StatusCode==HttpStatusCode.NotFound) {
            if(await Error(response,ct) is {} absent && absent.TryGetProperty("code",out var code)&&code.GetString()=="brainstorming.operation_missing")return default;
            captured.AccessRevoked?.Invoke();
            throw new HttpRequestException("Operation absence could not be confirmed. Check access or update the backend; the retained send will not be replayed.",null,response.StatusCode);
        }
        await Success(response,ct,captured.AccessRevoked);await using var stream=await response.Content.ReadAsStreamAsync(ct);using var bytes=new MemoryStream();var block=new byte[8192];int n;
        while((n=await stream.ReadAsync(block,ct))>0) {if(bytes.Length+n>8_000_000)throw new InvalidDataException("Oversized conversation response.");bytes.Write(block,0,n);captured.RequireCurrent();}
        captured.RequireCurrent();return JsonSerializer.Deserialize<T>(bytes.ToArray(),BrainstormingValidation.Json)??throw new InvalidDataException("Missing conversation response.");
    }
    public async Task<BrainstormingServerContext> PrepareAsync(BrainstormingServerContextRequest request,CancellationToken ct=default) {
        using var message=new HttpRequestMessage(HttpMethod.Post,Route+"/context") {Content=JsonContent.Create(request,options:BrainstormingValidation.Json)};
        var value=(await Json<BrainstormingServerContext>(request.Scope,message,false,ct))!;
        if(value.Preparation.Context is { } context) {BrainstormingValidation.Validate(context,request.Scope);if(value.Preparation.Resolution.Target is not { } target)throw new InvalidDataException("Missing server context target.");BrainstormingValidation.Validate(target,request.Scope.DocumentId);}
        if(value.Documents.Count>11)throw new InvalidDataException("Oversized server context.");foreach(var document in value.Documents)BrainstormingStorySources.Validate(document);
        return value;
    }
    public async IAsyncEnumerable<BrainstormingConversationEvent> SendAsync(BrainstormingSend send,[EnumeratorCancellation]CancellationToken ct=default) {
        BrainstormingConversationContract.Validate(send);var captured=lease(send.Scope);captured.RequireCurrent();
        using var request=new HttpRequestMessage(HttpMethod.Post,Route+"/stream") {Content=JsonContent.Create(send,options:BrainstormingValidation.Json)};captured.Configure?.Invoke(request);
        // Browser fetch otherwise buffers the response, defeating real streaming.
        request.Options.Set(new HttpRequestOptionsKey<bool>("WebAssemblyEnableStreamingResponse"),true);
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);captured.RequireCurrent();
        await Success(response,ct,captured.AccessRevoked,send.OperationId);
        if(response.Content.Headers.ContentType?.MediaType!="application/x-ndjson")throw new InvalidDataException("Update the backend for conversation streaming.");
        await using var stream=await response.Content.ReadAsStreamAsync(ct);using var reader=new StreamReader(stream,Encoding.UTF8);
        long sequence=0;bool terminal=false;var seen=new Dictionary<long,string>();
        while(await ReadLine(reader,ct) is { } line) {
            captured.RequireCurrent();ct.ThrowIfCancellationRequested();
            var item=JsonSerializer.Deserialize<BrainstormingConversationEvent>(line,BrainstormingValidation.Json)??throw new InvalidDataException("Missing stream event.");
            if(seen.TryGetValue(item.Sequence,out var prior)) {if(prior!=line)throw new InvalidDataException("Changed duplicate event.");continue;}
            if(terminal)throw new InvalidDataException("Late output after terminal event.");BrainstormingConversationContract.ValidateEvent(item,send,sequence);seen.Add(item.Sequence,line);sequence=item.Sequence;
            terminal=item.Kind is BrainstormingEventKind.Completed or BrainstormingEventKind.Cancelled or BrainstormingEventKind.Failed;
            captured.RequireCurrent();yield return item;
        }
        if(!terminal)throw new IOException("Conversation disconnected. Inspect the saved operation; do not resend automatically.");
    }
    private static async Task<string?> ReadLine(StreamReader reader,CancellationToken ct) {
        var line=new StringBuilder();var c=new char[1];
        while(await reader.ReadAsync(c.AsMemory(),ct)>0) {if(c[0]=='\n')return line.ToString();if(line.Length>1_000_000)throw new InvalidDataException("Oversized conversation event.");line.Append(c[0]);}
        if(line.Length!=0)throw new IOException("Incomplete conversation event.");return null;
    }
    public async Task<BrainstormingGeneration?> InspectAsync(BrainstormingScope scope,Guid operationId,CancellationToken ct=default) {
        using var message=new HttpRequestMessage(HttpMethod.Get,$"{Route}/{scope.SessionId}/{operationId}?documentId={scope.DocumentId}&projectId={scope.ProjectId}");
        var value=await Json<BrainstormingGeneration>(scope,message,true,ct);if(value is null)return null;
        BrainstormingConversationContract.Validate(value.Send);
        if(value.Version!=1 || value.Send.Scope!=scope || value.Send.OperationId!=operationId || value.InspectionText.Length>BrainstormingConversationContract.MaxOutput || value.Events.Count>BrainstormingConversationContract.MaxEvents+1)throw new InvalidDataException("Invalid retained generation.");
        long sequence=0;foreach(var item in value.Events) {BrainstormingConversationContract.ValidateEvent(item,value.Send,sequence);sequence++;}return value;
    }
    public async Task StopAsync(BrainstormingScope scope,Guid operationId,CancellationToken ct=default) {
        var captured=lease(scope);captured.RequireCurrent();using var request=new HttpRequestMessage(HttpMethod.Post,$"{Route}/{scope.SessionId}/{operationId}/stop") {Content=JsonContent.Create(scope,options:BrainstormingValidation.Json)};captured.Configure?.Invoke(request);
        using var response=await http.SendAsync(request,ct);captured.RequireCurrent();await Success(response,ct,captured.AccessRevoked);
    }
}
