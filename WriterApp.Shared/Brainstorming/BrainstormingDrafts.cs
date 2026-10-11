namespace WriterApp.Shared.Brainstorming;

public sealed record BrainstormingDraftSelection(int Version, Guid CandidateId, string CandidateFingerprint,
    string TextMapping, int From, int To, string Quote);
public sealed record BrainstormingDraftReview(int Version, Guid CandidateId, bool UseForNextRequest,
    BrainstormingDraftSelection? Selection = null);
public sealed record BrainstormingDraftEdit(int Version, Guid CandidateId, string BaseFingerprint,
    string Html, string PlainText, DateTimeOffset UpdatedAt);

public static class BrainstormingDrafts
{
    public static IReadOnlyList<BrainstormingDraft> Versions(BrainstormingSession session, Guid? branch) => session.Drafts.Where(d=>d.BranchId==branch).ToArray();
    public static IReadOnlyList<BrainstormingDraft> Branches(BrainstormingSession session) => session.Drafts.GroupBy(d=>d.BranchId).Select(g=>g.Last()).ToArray();
    public static string BranchLabel(BrainstormingDraft draft) => draft.BranchId is null ? "Main draft" : draft.BranchName ?? "Alternative";
    public static (string Text,string? Html) Compose(BrainstormingDraft? parent,BrainstormingProse prose) {
        if(!prose.Continuation)return(prose.Text,null);
        if(parent is null)throw new InvalidDataException("Continuation requires a reviewed working draft.");
        var html=(parent.EditorHtml??BrainstormingDraftContent.Html(parent.PlainText))+BrainstormingDraftContent.Html(prose.Text);
        BrainstormingDraftContent.RequireHtml(html);
        var text=BrainstormingDraftContent.PlainText(html);
        if(html.Length>100_000 || text.Length>100_000 || !text.StartsWith(parent.PlainText,StringComparison.Ordinal))throw new InvalidDataException("The continued draft exceeds its limit or changes the reviewed text.");
        return(text,html);
    }
    public const string TextMapping = "prosa-quality-utf16-v1";
    public static BrainstormingDraft? Candidate(BrainstormingCache cache) => cache.DraftReview is { } review
        ? cache.Session.Drafts.SingleOrDefault(d=>d.Id==review.CandidateId) : cache.Session.Drafts.LastOrDefault();
    public static void RequireSelection(BrainstormingDraftSelection selection, BrainstormingDraft draft) {
        if(selection.Version!=1 || selection.CandidateId!=draft.Id || selection.CandidateFingerprint!=BrainstormingValidation.CandidateFingerprint(draft)
            || selection.TextMapping!=TextMapping || selection.From<0 || selection.To<=selection.From || selection.To>draft.PlainText.Length
            || string.IsNullOrWhiteSpace(selection.Quote) || selection.Quote.Length>8000 || draft.PlainText[selection.From..selection.To]!=selection.Quote
            || SplitsSurrogate(draft.PlainText,selection.From) || SplitsSurrogate(draft.PlainText,selection.To))
            throw new InvalidDataException("The selected proposal passage changed or was lost. Select it again in the reviewed version.");
    }
    private static bool SplitsSurrogate(string text,int index) => index>0 && index<text.Length && char.IsHighSurrogate(text[index-1]) && char.IsLowSurrogate(text[index]);
    public static void ValidateReview(BrainstormingCache cache) {
        if(cache.DraftReview is { } review) {
            var candidate=Candidate(cache);
            if(review.Version!=1 || candidate is null)throw new InvalidDataException("Reviewed draft is unavailable. Retain the original cache.");
            if(review.Selection is { } selection)RequireSelection(selection,candidate);
        }
        if(cache.DraftEdit is { } edit) {
            var parent=cache.Session.Drafts.SingleOrDefault(d=>d.Id==edit.CandidateId);
            if(edit.Version!=1 || parent is null || edit.BaseFingerprint!=BrainstormingValidation.CandidateFingerprint(parent)
                || edit.UpdatedAt==default || edit.UpdatedAt>DateTimeOffset.UtcNow.AddMinutes(1) || edit.PlainText is null || edit.PlainText.Length>100_000 || edit.Html is null)
                throw new InvalidDataException("Proposal edit source changed. Its original is retained.");
            BrainstormingDraftContent.RequireHtml(edit.Html);
            if(BrainstormingDraftContent.PlainText(edit.Html)!=edit.PlainText)throw new InvalidDataException("Proposal text and rich editor snapshot disagree.");
        }
    }
    public static bool SameDestination(BrainstormingTarget original,BrainstormingTarget reviewed) => original==reviewed with {Id=original.Id};
    public static bool SameLocation(BrainstormingTarget original,BrainstormingTarget reviewed) => original==reviewed with {Id=original.Id,Source=original.Source};
}
