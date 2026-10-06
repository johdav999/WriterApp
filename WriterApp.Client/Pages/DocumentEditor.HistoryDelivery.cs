using System.Text.Json;
using Microsoft.AspNetCore.Components;
using WriterApp.Client.Services;
using WriterApp.Shared;
using WriterApp.UI.Shared;

namespace WriterApp.Client.Pages;
public partial class DocumentEditor
{
    [Inject] public WebAiHistoryOutbox? AiHistoryOutbox {get;set;}
    private WebAiHistoryOutbox? _fallbackHistory;
    private WebAiHistoryOutbox HistoryOutbox=>AiHistoryOutbox ?? (_fallbackHistory ??= new(Http,JSRuntime,Checked));
    private IReadOnlyList<WebAiHistoryDeliveryItem> _historyDelivery=[];
    private string? _historyDeliveryError;
    private readonly Dictionary<string,Guid> _qualityHistoryProposals=new();
    private readonly Dictionary<string,Guid> _continuityHistoryProposals=new();
    private bool _historyDeliveryBusy;
    private System.Threading.Timer? _historyDeliveryTimer;
    private WebAiHistoryIntent? _sceneHistoryIntent;
    private async Task<HttpResponseMessage> SaveSceneWithHistory(string url,object payload,WebCheckedAi.Lease lease) {
        if(_sceneAiProposalId is not { } proposal)throw new InvalidOperationException("Scene Apply has no owned proposal.");
        if(_sceneHistoryIntent?.ProposalId!=proposal || _sceneHistoryIntent.AfterContent!=JsonSerializer.Serialize(payload,WebAiHistoryContracts.Json)) {
            using var response=await Http.GetAsync(url);response.EnsureSuccessStatusCode();
            string original=IsSceneRoute?WebSceneCardSources.Canonical(await WebAiSources.Read<WriterApp.Application.Documents.SceneCardDto>(response.Content))
                :WebSceneCardSources.Canonical(await WebAiSources.Read<WriterApp.Application.Documents.SectionSceneCardDto>(response.Content));
            var id=Guid.NewGuid();_sceneHistoryIntent=new(1,id,id,proposal,1,null,"Applied",lease.Source,IsSceneRoute?"SceneCard":"SectionCard",original,JsonSerializer.Serialize(payload,WebAiHistoryContracts.Json));
        }
        using var message=Checked.Mutation(HttpMethod.Put,url,payload,lease);
        await HistoryOutbox.Attach(message,_sceneHistoryIntent,lease,CancellationToken.None);
        var saved=await Http.SendAsync(message);
        try {if(saved.IsSuccessStatusCode){await HistoryOutbox.Saved(_sceneHistoryIntent,lease,saved);_sceneHistoryIntent=null;}return saved;}
        catch{saved.Dispose();throw;}
    }
    private void StartHistoryDelivery() {
        _historyDeliveryTimer ??= new(_=>_ = InvokeAsync(RetryHistoryDelivery),null,TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(30));
        _=RefreshHistoryDelivery(true);
    }
    private async Task RetryHistoryDelivery() {await RefreshHistoryDelivery(true);await LoadAiHistoryAsync();await InvokeAsync(StateHasChanged);}
    private async Task RefreshHistoryDelivery(bool retry=false) {
        if(_historyDeliveryBusy)return;_historyDeliveryBusy=true;
        try {
            var lease=await Checked.Capture(DocumentId);
            if(retry)await HistoryOutbox.Flush(lease);
            var entries=await HistoryOutbox.List(lease);
            Checked.RequireLease(lease);
            _historyDelivery=entries.Select(entry=>{
                var intent=JsonSerializer.Deserialize<WebAiHistoryIntent>(entry.Json,WebAiHistoryContracts.Json)!;
                WebAiHistoryContracts.Validate(intent);
                return new WebAiHistoryDeliveryItem(intent.OperationId,intent.Outcome,intent.TargetKind,intent.Sequence,entry.Status,entry.Message,
                    intent.TargetKind=="Aggregate"?null:intent.BeforeContent,intent.TargetKind=="Aggregate"?null:intent.AfterContent,
                    entry.Receipt is not null && JsonSerializer.Deserialize<WebAiHistoryReceipt>(entry.Receipt,WebAiHistoryContracts.Json)?.CommittedAt is not null);
            }).ToArray();_historyDeliveryError=null;
        }catch(Exception e) when(e is InvalidOperationException or InvalidDataException or HttpRequestException or JsonException or Microsoft.JSInterop.JSException) {
            _historyDelivery=[];_historyDeliveryError=e.Message;
        }finally{_historyDeliveryBusy=false;}
    }
    private async Task PrepareAggregateHistory(PendingAiProposal pending,WebTranslationPreview preview) {
        if(!_checkedProposals.TryGetValue(pending.ProposalId,out var context))throw new InvalidOperationException("Aggregate history needs the original checked proposal. Generate again.");
        var intent=new WebAiHistoryIntent(1,preview.OperationId,preview.OperationId,pending.ProposalId,1,null,"Applied",context.Lease.Source,"Aggregate",null,null,preview.OperationId);
        await HistoryOutbox.Prepare(intent,context.Lease);
    }
}
