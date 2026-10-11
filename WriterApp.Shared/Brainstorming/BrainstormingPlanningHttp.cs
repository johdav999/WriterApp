using System.Net.Http.Json;

namespace WriterApp.Shared.Brainstorming;

public sealed class BrainstormingPlanningHttp(HttpClient http,Func<BrainstormingScope,BrainstormingRemoteLease> lease)
{
    private async Task<T> Send<T>(BrainstormingScope scope,string suffix,object body,CancellationToken ct) {
        var guard=lease(scope);guard.RequireCurrent();using var request=new HttpRequestMessage(HttpMethod.Post,$"api/brainstorming/{scope.SessionId}/planning/{suffix}") {Content=JsonContent.Create(body,body.GetType(),options:BrainstormingValidation.Json)};
        guard.Configure?.Invoke(request);using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);guard.RequireCurrent();
        if(response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.NotFound){guard.AccessRevoked?.Invoke();throw new OperationCanceledException("Planning access is unavailable. Reopen the original account; writing and journals are retained.");}
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException($"Planning operation unconfirmed (HTTP {(int)response.StatusCode}). Inspect before retry; stale planning needs fresh review.");
        var result=await WriterApp.Shared.WebAiSources.Read<T>(response.Content,ct);guard.RequireCurrent();return result;
    }
    public Task<BrainstormingPlanningSnapshot> CaptureAsync(BrainstormingScope s,CancellationToken ct=default)=>Send<BrainstormingPlanningSnapshot>(s,"snapshot",s,ct);
    public Task<BrainstormingPlanningReceipt> ApplyAsync(BrainstormingPlanningReview r,CancellationToken ct=default)=>Send<BrainstormingPlanningReceipt>(r.Scope,"apply",r,ct);
    public Task<BrainstormingPlanningReceipt> InspectAsync(BrainstormingPlanningReview r,CancellationToken ct=default)=>Send<BrainstormingPlanningReceipt>(r.Scope,"inspect",r,ct);
    public Task<BrainstormingPlanningReceipt> UndoAsync(BrainstormingPlanningReview r,CancellationToken ct=default)=>Send<BrainstormingPlanningReceipt>(r.Scope,"undo",r,ct);
}
