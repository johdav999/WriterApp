using System.Net.Http.Json;
using WriterApp.Application.AI;
using WriterApp.Shared;

namespace WriterApp.Client.Pages;

public partial class DocumentEditor
{
    private sealed record WebWritingContext(WritingOutlineSnapshot Source,int AccountGeneration,long Epoch,string Backend,Guid? SectionId);
    private long _writingEpoch;
    private readonly Dictionary<Guid,WebWritingContext> _writingContexts=new();
    private bool WritingContextCurrent(WebWritingContext context) => context.Epoch==_writingEpoch
        && TranslationContextCurrent(context.AccountGeneration,context.Source.DocumentId,context.Backend) && _activeSection?.Id==context.SectionId;
    private async Task<WritingOutlineSnapshot> ReadSavedWritingOutline(Guid id,CancellationToken ct)
    {
        using var response=await Http.GetAsync($"api/ai/actions/writing-outline/{id}",ct);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Saved writing outline is unavailable. Save the document, resolve conflicts or update the backend before generating.");
        if(response.Content.Headers.ContentLength>WritingOutline.MaxJsonCharacters*6)throw new InvalidDataException("Saved outline response exceeds the supported limit.");
        await using var stream=await response.Content.ReadAsStreamAsync(ct);using var buffer=new MemoryStream();var block=new byte[16384];int read;
        while((read=await stream.ReadAsync(block,ct))>0){if(buffer.Length+read>WritingOutline.MaxJsonCharacters*6)throw new InvalidDataException("Saved outline response exceeds the supported limit.");await buffer.WriteAsync(block.AsMemory(0,read),ct);}
        var outline=System.Text.Json.JsonSerializer.Deserialize<WritingOutlineSnapshot>(buffer.ToArray(),new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web){MaxDepth=16})
            ?? throw new InvalidDataException("Missing saved outline.");
        WritingOutline.Validate(outline);
        if(outline.DocumentId!=id)throw new InvalidDataException("Outline belongs to another document.");return outline;
    }
    private async Task<WebWritingContext> CaptureWritingContext(AiActionExecuteRequestDto request,CancellationToken ct)
    {
        var id=request.DocumentId ?? throw new InvalidOperationException("Choose a saved manuscript.");
        if(request.WritingOutline is { } retained) {
            var original=_writingContexts.Values.FirstOrDefault(c=>c.Source.Fingerprint==retained.Fingerprint && c.Source.DocumentVersion==retained.DocumentVersion && WritingContextCurrent(c));
            if(original is null)throw new InvalidOperationException("The retry lost its reviewed saved outline. Generate again.");
            var current=await ReadSavedWritingOutline(id,ct);
            if(!WritingContextCurrent(original) || current.Fingerprint!=retained.Fingerprint)
                throw new InvalidOperationException("Saved outline changed before retry. Generate a new proposal.");
            return original;
        }
        long epoch=++_writingEpoch;int generation=_webTranslationGeneration;string backend=Http.BaseAddress?.AbsoluteUri ?? "";
        _writingContexts.Clear();
        var outline=await ReadSavedWritingOutline(id,ct);
        var context=new WebWritingContext(outline,generation,epoch,backend,request.SectionId);
        if(!WritingContextCurrent(context))throw new InvalidOperationException("Account, backend or section changed while capturing the outline.");
        return context;
    }
    private bool HasCurrentWritingContext(Guid proposal) => _writingContexts.TryGetValue(proposal,out var context) && WritingContextCurrent(context);
    private async Task RetainWritingContext(HttpResponseMessage response,WebWritingContext context,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(!WritingContextCurrent(context))throw new InvalidOperationException("The writing request context changed. Generate again.");
        var result=await WebAiSources.Read<AiActionExecuteResponseDto>(response.Content,ct);
        if(result is null || result.ProposalId==Guid.Empty || result.SourceOutlineFingerprint!=context.Source.Fingerprint
            || result.SourceDocumentVersion!=context.Source.DocumentVersion)throw new InvalidDataException("Backend did not confirm the saved writing outline.");
        var current=await ReadSavedWritingOutline(context.Source.DocumentId,ct);
        if(!WritingContextCurrent(context) || current.Fingerprint!=context.Source.Fingerprint)
            throw new InvalidOperationException("Saved writing or outline changed during generation. Review a new proposal.");
        RequireClientAiRequest(ct);
        _writingContexts[result.ProposalId]=context;
    }
    private async Task<bool> ValidateWritingApproval(PendingAiProposal pending)
    {
        if(!await ValidateCheckedApproval(pending))return false;
        if(!WritingOutline.Consumes(pending.ActionKey))return true;
        try {
            if(!_writingContexts.TryGetValue(pending.ProposalId,out var context) || !WritingContextCurrent(context))
                throw new InvalidOperationException("This proposal has no current saved outline approval. Generate and review again.");
            var current=await ReadSavedWritingOutline(context.Source.DocumentId,default);
            if(!WritingContextCurrent(context) || current.Fingerprint!=context.Source.Fingerprint)
                throw new InvalidOperationException("Saved writing or outline changed. Generate and review again before applying.");
            return true;
        } catch(Exception e) when(e is InvalidOperationException or InvalidDataException or HttpRequestException or System.Text.Json.JsonException) {
            _pendingAiProposal=pending with{ErrorMessage=e.Message};await InvokeAsync(StateHasChanged);return false;
        }
    }
}
