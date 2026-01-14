using System;

namespace WriterApp.Application.Commands
{
    public sealed record AiTextProposal(string ProposedText, string? Explanation);

    public interface IAiTextService
    {
        AiTextProposal ProposeText(
            Guid sectionId,
            TextRange selectionRange,
            string originalText,
            string instruction,
            AiActionScope scope);
    }
}
