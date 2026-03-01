using System;

namespace WriterApp.Application.Commands
{
    public interface IAiEditCommand : IDocumentCommand
    {
        Guid CommandId { get; }
        Guid SectionId { get; }
        Guid AiEditGroupId { get; }
        string? AiEditGroupReason { get; }
        DateTime AppliedUtc { get; }
    }
}
