using System.Text.Json;
using WriterApp.Application.AI;
using WriterApp.Shared;

namespace WriterApp.Client.Pages;
public partial class DocumentEditor
{
    private sealed record WebWritingPreview(WebTranslationSource Source,WritingStructure Original,WritingStructure Proposed,Guid Operation);
    private readonly Dictionary<Guid,WebWritingPreview> _checkedWritingPreviews=new();
    private async Task<(AiActionExecuteRequestDto Request,WebTranslationSource? Source,WritingStructure? Structure)> CaptureStructuredWriting(string key,AiActionExecuteRequestDto request,CancellationToken ct)
    {
        bool section=WritingActions.SectionKeys.Contains(key) || key=="custom_transform" && request.SelectionStart is null;
        if(!section) return(request,null,null);
        if(IsSceneRoute)throw new InvalidOperationException("Checked multi-page writing requires the linked manuscript section. Open that section for this action; selection actions remain available here.");
        var source=await TranslationSource(request.DocumentId!.Value,"section",request.SectionId,ct);
        await ConfirmActiveTranslationSave(source,ct);
        var pages=new List<TranslationPage>();
        foreach(var page in source.Sections.Single().Pages) {
            var runs=await JSRuntime.InvokeAsync<CapturedRuns>("tiptapEditor.captureTranslation",ct,[page.Content,"Html"]);
            RequireClientAiRequest(ct);
            pages.Add(new(page.Id,runs.Runs));
        }
        var structure=new WritingStructure(1,source.DocumentId,source.Sections.Single().Id,pages);
        var parameters=request.Parameters is null ? new Dictionary<string,object?>() : new(request.Parameters);
        var recommended = RecommendedWriting.From(parameters);
        if (recommended is not null) {
            RecommendedWriting.ValidateSource(structure, recommended.ToolId);
            RecommendedWriting.ValidateOpeningHtml(source.Sections.Single().Pages[0].Content, recommended.ToolId);
            RecommendedWriting.ValidateParameters(parameters, recommended);
            if (_aiUsageStatus?.SupportsRecommendedWriting != true) throw new InvalidOperationException("Update or refresh the backend to support recommended writing tools.");
            if (RecommendedWriting.Output(recommended.ToolId) == RecommendedOutput.AppendParagraph && request.PageId != pages.Last().Id)
                throw new InvalidOperationException("This tool appends at the section end. Open the last page in this section, then run it again.");
        }
        if(key=="custom_transform" && !parameters.ContainsKey(ReusablePrompts.Parameter) && recommended is null)throw new InvalidOperationException("This custom tool has no checked section contract. Save it as a supported reusable preset or choose a selection action.");
        parameters[WritingActions.Parameter]=WritingActions.Serialize(structure);
        return(request with { Parameters=parameters,ExpectedDocumentVersion=source.DocumentVersion,SurroundingText=string.Join("\n\n",pages.Select(p=>string.Join("\n",p.Runs.Select(r=>r.Text)))) },source,structure);
    }
    private async Task ApplyCheckedWriting(PendingAiProposal pending,WebWritingPreview writing)
    {
        var original=new TranslationStructure(1,writing.Source.DocumentId,"section","en",[new(writing.Original.SectionId,writing.Original.Pages)]);
        var proposed=original with { Sections=[new(writing.Proposed.SectionId,writing.Proposed.Pages)] };
        var preview=new WebTranslationPreview(pending.ProposalId,writing.Operation,Http.BaseAddress?.AbsoluteUri ?? "",writing.Source,original,proposed,"auto",[]);
        _webTranslationPreview=preview;
        await ApplyStructuredTranslationAsync(pending);
    }
}
