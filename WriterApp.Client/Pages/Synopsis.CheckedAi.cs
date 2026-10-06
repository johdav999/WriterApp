using System.Net.Http.Json;
using WriterApp.Application.Synopsis;
using WriterApp.Client.Services;
using WriterApp.Shared;
using WriterApp.Shared.Sync;

namespace WriterApp.Client.Pages;
public partial class Synopsis
{
    [Microsoft.AspNetCore.Components.Inject] public WebCheckedAi CheckedAi { get; set; } = default!;
    [Microsoft.AspNetCore.Components.Inject] public WebAiHistoryOutbox HistoryOutbox { get; set; } = default!;
    private WebCheckedAi.Lease? _synopsisAiLease;
    private SynopsisAiRequestDto? _synopsisAiRequest;
    private SynopsisAiResponseDto? _synopsisAiResponse;
    private Guid? _synopsisProject;
    private SyncSynopsis? _savedSynopsisSource;
    private static SyncSynopsis SynopsisSnapshot(DocumentSynopsisDto s)=>new(s.Logline,s.Premise,s.Theme,s.ProtagonistArc,s.CentralConflict,s.Stakes,s.Setting,s.EndingIntent,s.OpenQuestions,s.Notes);
    private async Task RequireSynopsisApproval()
    {
        if(_synopsisAiLease is null || _synopsisAiRequest is null || _synopsisAiResponse is null || _synopsis is null
            || DocumentId!=_synopsisAiLease.Source.DocumentId || _synopsisAiRequest.FocusFieldKey!=_aiSelectedFieldKey
            || SynopsisSnapshot(_synopsis)!=_synopsisAiRequest.SourceSynopsis) throw new InvalidOperationException("Synopsis or field changed. Save and generate again.");
        if(_synopsisAiLease.Backend!=(Http.BaseAddress?.AbsoluteUri ?? ""))throw new InvalidOperationException("Synopsis backend changed. Generate again.");
        WebAiSources.RequireTime(_synopsisAiResponse.CreatedUtc);
        await CheckedAi.Confirm(_synopsisAiLease);
        if(DocumentId!=_synopsisAiLease.Source.DocumentId || _synopsis is null || SynopsisSnapshot(_synopsis)!=_synopsisAiRequest.SourceSynopsis || _synopsisAiRequest.FocusFieldKey!=_aiSelectedFieldKey)
            throw new InvalidOperationException("Synopsis changed during source validation. Generate again.");
    }
}
