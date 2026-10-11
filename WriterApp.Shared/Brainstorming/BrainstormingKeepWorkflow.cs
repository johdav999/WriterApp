using System.Net.Http.Json;
using System.Text.Json;

namespace WriterApp.Shared.Brainstorming;

public sealed class BrainstormingKeepWorkflow(IBrainstormingCacheStore cache)
{
    public async Task<BrainstormingCache> RememberAsync(BrainstormingScope scope,BrainstormingKeepReview review,BrainstormingKeepReceipt? receipt=null,CancellationToken ct=default)
    {
        var current=await cache.ReadAsync(scope,ct)??throw new InvalidDataException("Conversation unavailable.");
        BrainstormingKeepContract.Validate(review);
        var old=(current.Keeps??[]).SingleOrDefault(k=>k.Review.Candidate.Id==review.Candidate.Id);
        if(old is not null && BrainstormingKeepContract.Hash(old.Review)!=BrainstormingKeepContract.Hash(review))
            throw new InvalidOperationException("This candidate already has an application identity. Inspect its original operation.");
        if(receipt is not null)BrainstormingKeepContract.RequireReceipt(review,receipt);
        return await cache.WriteAsync(current with {Keeps=[..(current.Keeps??[]).Where(k=>k.Review.OperationId!=review.OperationId),new(review,receipt??old?.Receipt)]},current.CacheRevision,Guid.NewGuid(),ct);
    }
    public async Task<BrainstormingCache> ApplyAsync(BrainstormingScope local,BrainstormingKeepReview review,
        Func<BrainstormingKeepReview,Task<BrainstormingKeepReceipt>> apply,CancellationToken ct=default)
    {
        var saved=await RememberAsync(local,review,ct:ct);
        var previous=saved.Keeps!.Single(k=>k.Review.OperationId==review.OperationId).Receipt?.State;
        if(previous is BrainstormingApplicationState.Committed or BrainstormingApplicationState.Undone)return saved;
        if(previous==BrainstormingApplicationState.Unknown)throw new InvalidOperationException("Inspect the uncertain operation before an explicit retry.");
        // Durable uncertain intent precedes dispatch; reconnect/open only inspects, never invokes this delegate.
        saved=await RememberAsync(local,review,new(1,review.OperationId,review.Scope,review.Candidate.Id,BrainstormingKeepContract.Hash(review),BrainstormingApplicationState.Unknown,review.OperationId,0,"Approved operation awaiting inspection; no success inferred.",DateTimeOffset.UtcNow),ct);
        var receipt=await apply(review);return await RememberAsync(local,review,receipt,ct);
    }
}

public sealed class BrainstormingKeepHttp(HttpClient http,Func<BrainstormingScope,BrainstormingRemoteLease> lease)
{
    private static string Path(BrainstormingScope s,Guid operation)=>$"api/brainstorming/{s.SessionId}/keeps/{operation}?documentId={s.DocumentId}&projectId={s.ProjectId}";
    private async Task<BrainstormingKeepReceipt> Send(BrainstormingScope scope,string path,HttpMethod method,object? body,CancellationToken ct)
    {
        var guard=lease(scope);guard.RequireCurrent();using var request=new HttpRequestMessage(method,path);
        if(body is not null)request.Content=JsonContent.Create(body,body.GetType(),options:BrainstormingValidation.Json);
        guard.Configure?.Invoke(request);using var result=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);guard.RequireCurrent();
        if(result.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.NotFound){guard.AccessRevoked?.Invoke();throw new OperationCanceledException("Application access is unavailable. Reopen the original account; writing and journals are retained.");}
        if(!result.IsSuccessStatusCode)throw new InvalidOperationException($"Application is unconfirmed (HTTP {(int)result.StatusCode}). Inspect the retained operation; stale writing requires a fresh proposal.");
        await using var stream=await result.Content.ReadAsStreamAsync(ct);using var bounded=new MemoryStream();var buffer=new byte[4096];int read;
        while((read=await stream.ReadAsync(buffer,ct))>0){if(bounded.Length+read>32_768)throw new InvalidDataException("Oversized application receipt.");bounded.Write(buffer,0,read);}guard.RequireCurrent();var bytes=bounded.ToArray();
        return JsonSerializer.Deserialize<BrainstormingKeepReceipt>(bytes,BrainstormingValidation.Json)??throw new InvalidDataException("Missing application receipt.");
    }
    public async Task<BrainstormingKeepReceipt> ApplyAsync(BrainstormingKeepReview review,CancellationToken ct=default)
    {
        await Send(review.Scope,Path(review.Scope,review.OperationId),HttpMethod.Post,review,ct);
        return await Send(review.Scope,Path(review.Scope,review.OperationId).Replace("?","/commit?"),HttpMethod.Post,review.Scope,ct);
    }
    public Task<BrainstormingKeepReceipt> InspectAsync(BrainstormingKeepReview review,CancellationToken ct=default)=>Send(review.Scope,Path(review.Scope,review.OperationId).Replace("?","/inspect?"),HttpMethod.Post,review,ct);
    public Task<BrainstormingKeepReceipt> MoveAsync(BrainstormingKeepReview review,BrainstormingKeepMove move,CancellationToken ct=default)=>Send(review.Scope,Path(review.Scope,review.OperationId).Replace("?","/move?"),HttpMethod.Post,move,ct);
    public async Task<Guid> RecoverAsync(BrainstormingKeepReview review,CancellationToken ct=default) {
        var guard=lease(review.Scope);guard.RequireCurrent();using var request=new HttpRequestMessage(HttpMethod.Post,Path(review.Scope,review.OperationId).Replace("?","/original?"));guard.Configure?.Invoke(request);
        using var response=await http.SendAsync(request,ct);guard.RequireCurrent();if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Original-copy recovery was not confirmed. Current manuscript is retained.");
        var id=await response.Content.ReadFromJsonAsync<Guid>(ct);guard.RequireCurrent();if(id==Guid.Empty)throw new InvalidDataException("Missing recovered document identity.");return id;
    }
}
