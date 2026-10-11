using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WriterApp.Shared.Brainstorming;

public sealed record BrainstormingRemoteLease(Action RequireCurrent, Action<HttpRequestMessage>? Configure = null,Action? AccessRevoked=null);
public sealed class BrainstormingHttpRemote(HttpClient http, Func<BrainstormingScope, BrainstormingRemoteLease> lease,
    Func<Guid, BrainstormingScope> documentScope) : IBrainstormingRemote,IBrainstormingRecoveryRemote
{
    private const string Route = "api/brainstorming/sessions";
    private async Task<T?> Send<T>(BrainstormingScope scope, HttpRequestMessage request, bool allowMissing, CancellationToken ct,int maxBytes=2_100_000)
    {
        var captured = lease(scope); captured.RequireCurrent(); captured.Configure?.Invoke(request);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        captured.RequireCurrent(); ct.ThrowIfCancellationRequested();
        await using var input = await response.Content.ReadAsStreamAsync(timeout.Token); using var bytes = new MemoryStream(); var block = new byte[8192]; int count;
        while ((count = await input.ReadAsync(block, timeout.Token)) > 0) { if (bytes.Length + count > maxBytes) throw new InvalidDataException("Oversized conversation response."); bytes.Write(block, 0, count); }
        captured.RequireCurrent(); ct.ThrowIfCancellationRequested();
        if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || response.StatusCode==HttpStatusCode.NotFound && System.Text.Encoding.UTF8.GetString(bytes.ToArray()).Contains("brainstorming.access_revoked",StringComparison.Ordinal)) {
            captured.AccessRevoked?.Invoke();throw new HttpRequestException("Conversation access was revoked. Sign in and reopen the original account; local writing is retained.",null,response.StatusCode);
        }
        if(response.StatusCode==HttpStatusCode.NotFound && allowMissing)return default;
        if (response.StatusCode == HttpStatusCode.Conflict) {
            using var error = JsonDocument.Parse(bytes.ToArray()); BrainstormingSession? current = null;
            if (error.RootElement.TryGetProperty("current", out var value) && value.ValueKind != JsonValueKind.Null) {
                current = value.Deserialize<BrainstormingSession>(BrainstormingValidation.Json); BrainstormingValidation.Validate(current!);
                BrainstormingValidation.RequireScope(current!.Scope, scope);
            }
            throw new BrainstormingConcurrencyException(current);
        }
        if (!response.IsSuccessStatusCode) {
            var message=(int)response.StatusCode>=500
                ? $"The backend cannot read or save conversations (HTTP {(int)response.StatusCode}). Your unsent text and operation are retained. Repair or update the backend's Brainstorm database, then retry."
                : $"Conversation save/read unavailable (HTTP {(int)response.StatusCode}). Your unsent text and operation are retained; reconnect or update the backend.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        return JsonSerializer.Deserialize<T>(bytes.ToArray(), BrainstormingValidation.Json) ?? throw new InvalidDataException("Missing conversation response.");
    }
    public async Task<BrainstormingSession?> ReadAsync(BrainstormingScope scope, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Route}/{scope.SessionId}?documentId={scope.DocumentId}");
        var session = await Send<BrainstormingSession>(scope, request, true, ct);
        if (session is not null) { BrainstormingValidation.Validate(session); BrainstormingValidation.RequireScope(session.Scope, scope); } return session;
    }
    public async Task<BrainstormingRecoveryBundle> ExchangeRecoveryAsync(BrainstormingScope scope,BrainstormingRecoveryCopy? copy,CancellationToken ct=default) {
        if(copy is not null)BrainstormingRecovery.Validate(copy,scope);
        using var request=new HttpRequestMessage(HttpMethod.Post,$"{Route}/{scope.SessionId}/recovery?documentId={scope.DocumentId}"){Content=JsonContent.Create(copy,options:BrainstormingValidation.Json)};
        var bundle=await Send<BrainstormingRecoveryBundle>(scope,request,false,ct,BrainstormingRecovery.MaxBytes)??throw new InvalidDataException("Recovery history unavailable.");BrainstormingRecovery.Validate(bundle,scope);return bundle;
    }
    public async Task<BrainstormingReceipt> WriteAsync(BrainstormingWrite value, CancellationToken ct = default)
    {
        BrainstormingPersistence.Validate(value);
        using var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = JsonContent.Create(value, options: BrainstormingValidation.Json) };
        var receipt = (await Send<BrainstormingReceipt>(value.Session.Scope, request, false, ct))!;
        BrainstormingPersistence.RequireReceipt(value, receipt); return receipt;
    }
    public async Task<BrainstormingSessionPage> ListAsync(Guid documentId, Guid? after = null, int limit = 25, CancellationToken ct = default)
    {
        if (limit is < 1 or > 50) throw new InvalidDataException("Use a page size of 1–50.");
        var scope = documentScope(documentId); using var request = new HttpRequestMessage(HttpMethod.Get, $"{Route}?documentId={documentId}&after={after}&limit={limit}");
        var page = (await Send<BrainstormingSessionPage>(scope, request, false, ct))!;
        if (page.Version != 1 || page.Items is null || page.Items.Count > limit || page.NextAfter == Guid.Empty) throw new InvalidDataException("Invalid conversation page.");
        foreach (var s in page.Items) {
            BrainstormingValidation.Validate(s.Scope);
            if (s.Scope.DocumentId != documentId || s.Scope.AccountKey != scope.AccountKey || s.Scope.BackendKey != scope.BackendKey
                || s.Scope.Space != BrainstormingIdentitySpace.Server || s.Revision < 1 || !Enum.IsDefined(s.State)) throw new InvalidDataException("Wrong conversation page scope.");
        }
        return page;
    }
    public async Task<BrainstormingReceipt?> InspectAsync(BrainstormingScope scope, Guid operationId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Route}/{scope.SessionId}/operations/{operationId}?documentId={scope.DocumentId}");
        var receipt = await Send<BrainstormingReceipt>(scope, request, true, ct);
        if (receipt is not null && (receipt.Version != 1 || receipt.Scope != scope || receipt.OperationId != operationId || receipt.Revision < 1
            || receipt.PayloadHash is not {Length:64} || receipt.SavedAt == default)) throw new InvalidDataException("Invalid conversation operation receipt.");
        return receipt;
    }
}
