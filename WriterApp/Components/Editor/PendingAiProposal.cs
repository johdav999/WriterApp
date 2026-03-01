using System;
using WriterApp.AI.Abstractions;

namespace BlazorApp.Components.Editor
{
    public sealed record PendingAiProposal(
        AiProposal? Proposal,
        string Instruction,
        string OriginalText,
        string? ProposedText,
        string? ImageDataUrl,
        DateTime CreatedUtc);
}
