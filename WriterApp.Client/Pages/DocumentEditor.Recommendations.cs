using Microsoft.JSInterop;
using WriterApp.Shared;
namespace WriterApp.Client.Pages;
public partial class DocumentEditor
{
    private sealed record RecommendedCopyPreview(Guid Proposal, string ToolId, RecommendedTextResult Result, WebProposal Checked);
    private RecommendedCopyPreview? _recommendedCopy;
    private string? _recommendedCopyMessage;
    private void DismissRecommendedCopy() { _recommendedCopy = null; _recommendedCopyMessage = null; }
    private async Task CopyRecommendedText(string text) {
        if (_recommendedCopy is not { } review || _clientAiRequest is not null || _isProposalApplying) return;
        try {
            if (!review.Result.Items.Contains(text)) throw new InvalidDataException("Choose a reviewed result.");
            await FlushActiveEditorAsync("recommended-copy");
            await RequireCheckedProposal(review.Checked);
            if (!ReferenceEquals(review, _recommendedCopy) || !HasCurrentWritingContext(review.Proposal)) throw new InvalidOperationException("The reviewed writing changed. Generate the result again.");
            var current = await ReadSavedWritingOutline(DocumentId, default);
            if (!ReferenceEquals(review, _recommendedCopy) || !HasCurrentWritingContext(review.Proposal)
                || !_writingContexts.TryGetValue(review.Proposal, out var context) || current.Fingerprint != context.Source.Fingerprint)
                throw new InvalidOperationException("The reviewed writing or saved outline changed. Generate again.");
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
            _recommendedCopyMessage = "Selected text copied. Your manuscript is unchanged.";
        } catch (Exception e) when (e is InvalidOperationException or InvalidDataException or HttpRequestException or JSException) { _recommendedCopyMessage = e.Message; }
    }
}
