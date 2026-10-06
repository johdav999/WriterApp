using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;

namespace WriterApp.Device.Shared.Services;

public static class LocalStyleQualityApproval
{
    public static LocalAiHistory Prepare(DeviceAiProposal proposal, IReadOnlyCollection<int> approved, LocalAiHistory history)
    {
        if (proposal.Prepared.Action != DeviceAiAction.StyleQuality || proposal.StyleReview is not { } report
            || proposal.SourceText != proposal.Prepared.SelectedText || history.OriginalText != proposal.SourceText
            || history.PageId != proposal.Prepared.LocalPageId || history.ServerVersion != proposal.Prepared.Request.ExpectedDocumentVersion)
            throw new InvalidDataException("The style review does not match its recovery source. Review again.");
        if (approved.Count == 0) throw new InvalidOperationException("Select a style change before applying.");
        string selected = StyleQualityReview.Compose(report, proposal.SourceText, approved);
        if (selected != proposal.ProposedText || selected == proposal.SourceText)
            throw new InvalidDataException("Review the selected style changes again.");
        // Keep the full proposal immutable. A partial approval gets its own durable recovery identity.
        return history.Proposed == selected ? history : history with {
            Id = Guid.NewGuid(), Proposed = selected, CreatedAt = DateTimeOffset.UtcNow, Deliveries = null
        };
    }
}
