using System;
using System.Collections.Generic;
using System.Threading;
using WriterApp.AI.Abstractions;

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

        IAsyncEnumerable<AiStreamEvent> StreamTextAsync(
            Guid sectionId,
            TextRange selectionRange,
            string originalText,
            string instruction,
            AiActionScope scope,
            CancellationToken ct);
    }
}
