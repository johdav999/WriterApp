using System;
using WriterApp.Application.Commands;

namespace BlazorApp.Components.Editor
{
    public sealed record PendingAiEdit(
        Guid SectionId,
        TextRange Range,
        string OriginalText,
        string ProposedText,
        string Instruction,
        DateTime CreatedUtc);
}
