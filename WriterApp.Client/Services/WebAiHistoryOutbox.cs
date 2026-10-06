using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using WriterApp.Shared;

namespace WriterApp.Client.Services;

public sealed class WebAiHistoryOutbox(HttpClient http,IJSRuntime js,WebCheckedAi checkedAi) : IAsyncDisposable
{
    public sealed record Entry(string OperationId,string Json,string Status,string? Message,string? Receipt);
    private Task<IJSObjectReference>? _module;
    private Task<IJSObjectReference> Module()=>_module ??= js.InvokeAsync<IJSObjectReference>("import","./js/web-ai-history-outbox.js").AsTask();
    private string Scope(WebCheckedAi.Lease lease) {checkedAi.RequireLease(lease);return lease.Backend+"|"+lease.Source.AccountKey;}
    public async Task Prepare(WebAiHistoryIntent intent,WebCheckedAi.Lease lease,CancellationToken ct=default)
    {
        WebAiHistoryContracts.Validate(intent);if(intent.Source!=lease.Source)throw new InvalidDataException("History intent targets another source.");
        var scope=Scope(lease);var module=await Module();
        await module.InvokeVoidAsync("prepare",ct,scope,JsonSerializer.Serialize(intent,WebAiHistoryContracts.Json));
        Scope(lease);using var response=await http.PostAsJsonAsync("api/ai/actions/history/web/operations",intent,ct);
        var receipt=await Read(response,ct);WebAiHistoryContracts.RequireReceipt(intent,receipt);Scope(lease);
        await Mark(lease,intent,receipt.Delivery,"Approved intent retained; content persistence is a separate step.",receipt,ct);
    }
    public async Task Attach(HttpRequestMessage message,WebAiHistoryIntent intent,WebCheckedAi.Lease lease,CancellationToken ct)
    {
        await Prepare(intent,lease,ct);Scope(lease);
        message.Headers.Add("X-WriterApp-AI-Operation",intent.OperationId.ToString());
    }
    public async Task Saved(WebAiHistoryIntent intent,WebCheckedAi.Lease lease,HttpResponseMessage response,CancellationToken ct=default)
    {
        Scope(lease);WebCheckedAi.RequireMutationReceipt(response);
        if(!response.Headers.TryGetValues("X-WriterApp-AI-Operation",out var ids) || !ids.Contains(intent.OperationId.ToString()))
            throw new InvalidDataException("Save has no matching durable receipt. Reload to reconcile; intent remains retained.");
        // A storage/reporting failure after the atomic save cannot roll back saved writing.
        try {
            var receipt=await Inspect(intent,lease,ct);
            if(receipt.CommittedAt is null)throw new InvalidDataException("AI Save has no committed history proof.");
            await Mark(lease,intent,receipt.Delivery,"Writing saved; history delivery pending.",receipt,ct);await Flush(lease,ct);
        } catch(Exception e) when(e is JSException or HttpRequestException or InvalidOperationException or InvalidDataException or JsonException or OperationCanceledException) { }
    }
    private async Task<WebAiHistoryReceipt> Inspect(WebAiHistoryIntent intent,WebCheckedAi.Lease lease,CancellationToken ct)
    {
        Scope(lease);using var response=await http.GetAsync($"api/ai/actions/history/web/operations/{intent.OperationId}",ct);
        var receipt=await Read(response,ct);WebAiHistoryContracts.RequireReceipt(intent,receipt);Scope(lease);return receipt;
    }
    public async Task<IReadOnlyList<Entry>> List(WebCheckedAi.Lease lease,CancellationToken ct=default)
    {
        var json=await (await Module()).InvokeAsync<string>("list",ct,Scope(lease),lease.Source.DocumentId.ToString());
        if(json.Length>20_000_000)throw new InvalidDataException("History outbox exceeds its inspection limit; retained data was not removed.");
        Scope(lease);var entries=JsonSerializer.Deserialize<Entry[]>(json,WebAiHistoryContracts.Json) ?? [];
        foreach(var entry in entries) {
            var intent=JsonSerializer.Deserialize<WebAiHistoryIntent>(entry.Json,WebAiHistoryContracts.Json) ?? throw new InvalidDataException("Stored history intent is malformed; its original is retained.");
            WebAiHistoryContracts.Validate(intent);
            if(entry.OperationId!=intent.OperationId.ToString() || intent.Source.DocumentId!=lease.Source.DocumentId || intent.Source.AccountKey!=lease.Source.AccountKey
                || entry.Status is not ("Prepared" or "Pending" or "Confirmed" or "Rejected" or "Uncertain"))throw new InvalidDataException("Stored history identity/state is invalid; its original is retained.");
            if(entry.Receipt is not null) {
                var receipt=JsonSerializer.Deserialize<WebAiHistoryReceipt>(entry.Receipt,WebAiHistoryContracts.Json) ?? throw new InvalidDataException("Stored history receipt is missing.");
                WebAiHistoryContracts.RequireReceipt(intent,receipt);
                if(entry.Status=="Confirmed" && receipt.Delivery!="Confirmed")throw new InvalidDataException("Stored delivery has no confirmed receipt.");
            } else if(entry.Status=="Confirmed")throw new InvalidDataException("Stored delivery has no confirmed receipt.");
        }
        return entries;
    }
    public async Task Flush(WebCheckedAi.Lease lease,CancellationToken ct=default)
    {
        var entries=await List(lease,ct);
        foreach(var entry in entries.Where(e=>e.Status!="Confirmed").OrderBy(e=>JsonSerializer.Deserialize<WebAiHistoryIntent>(e.Json,WebAiHistoryContracts.Json)!.Sequence)) {
            var intent=JsonSerializer.Deserialize<WebAiHistoryIntent>(entry.Json,WebAiHistoryContracts.Json)!;WebAiHistoryContracts.Validate(intent);
            if(intent.Source.AccountKey!=lease.Source.AccountKey || intent.Source.DocumentId!=lease.Source.DocumentId)throw new InvalidDataException("Outbox contains another account/target.");
            try {
                var receipt=await Inspect(intent,lease,ct);
                if(receipt.CommittedAt is null) {await Mark(lease,intent,"Prepared","Save is unconfirmed. Reload current writing and inspect retained intent before retrying Apply.",receipt,ct);continue;}
                if(receipt.Delivery!="Confirmed") {
                    Scope(lease);using var response=await http.PostAsJsonAsync($"api/ai/actions/history/web/operations/{intent.OperationId}/report",intent,ct);
                    receipt=await Read(response,ct);WebAiHistoryContracts.RequireReceipt(intent,receipt);Scope(lease);
                    if(receipt.Delivery!="Confirmed")throw new InvalidDataException("History delivery was not confirmed.");
                }
                await Mark(lease,intent,"Confirmed","Saved outcome and cloud history confirmed.",receipt,ct);
            } catch(OperationCanceledException){throw;}
            catch(Exception e) when(e is HttpRequestException or InvalidOperationException or InvalidDataException or JsonException) {
                Scope(lease);await Mark(lease,intent,e is HttpRequestException?"Uncertain":"Rejected",e.Message,null,ct);
            }
        }
    }
    private async Task Mark(WebCheckedAi.Lease lease,WebAiHistoryIntent intent,string state,string message,WebAiHistoryReceipt? receipt,CancellationToken ct)
        =>await (await Module()).InvokeVoidAsync("mark",ct,Scope(lease),intent.OperationId.ToString(),state,message,receipt is null?null:JsonSerializer.Serialize(receipt,WebAiHistoryContracts.Json));
    private static async Task<WebAiHistoryReceipt> Read(HttpResponseMessage response,CancellationToken ct)
    {
        if(!response.IsSuccessStatusCode) {
            string message="History receipt is unavailable. Saved content and intent are retained; reconnect/update the backend and retry delivery.";
            try {var error=await WebAiSources.Read<JsonElement>(response.Content,ct);if(error.TryGetProperty("message",out var text))message=text.GetString() ?? message;}catch(Exception e) when(e is JsonException or InvalidDataException){}
            throw new InvalidOperationException(message);
        }
        return await WebAiSources.Read<WebAiHistoryReceipt>(response.Content,ct);
    }
    public async ValueTask DisposeAsync(){if(_module is not null)try{await (await _module).DisposeAsync();}catch(JSDisconnectedException){} }
}
