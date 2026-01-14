using System;
using System.Collections.Generic;
using WriterApp.Application.Commands;
using WriterApp.Domain.Documents;

namespace WriterApp.AI.Abstractions
{
    public sealed record AiActionInput(
        Document Document,
        Guid ActiveSectionId,
        TextRange SelectionRange,
        string SelectedText,
        string? Instruction,
        Dictionary<string, object?>? Options);
}
