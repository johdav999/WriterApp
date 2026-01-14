using System;
using WriterApp.Application.Commands;

namespace WriterApp.AI.Abstractions
{
    public abstract record ProposedOperation;

    public sealed record ReplaceTextRangeOperation(Guid SectionId, TextRange Range, string NewText) : ProposedOperation;

    public sealed record AttachImageOperation(Guid SectionId, Guid ArtifactId, string Placement) : ProposedOperation;
}
