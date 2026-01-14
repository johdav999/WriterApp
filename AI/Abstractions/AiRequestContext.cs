using System;
using WriterApp.Application.Commands;

namespace WriterApp.AI.Abstractions
{
    public sealed record AiRequestContext(
        Guid DocumentId,
        Guid SectionId,
        TextRange Range,
        string OriginalText,
        string? DocumentTitle,
        string? OutlineText,
        string? SurroundingText,
        string? LanguageHint,
        string? SelectionText,
        int? SelectionStart,
        int? SelectionLength,
        string? ContainingParagraph,
        string? SurroundingBefore,
        string? SurroundingAfter);
}
