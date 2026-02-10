using System;
using System.Collections.Generic;

namespace WriterApp.Application.Documents
{
    public sealed record OutlineTemplateDto(
        Guid Id,
        string Name,
        DateTimeOffset CreatedUtc,
        DateTimeOffset UpdatedUtc,
        int NodeCount = 0,
        string? Description = null);

    public sealed record OutlineTemplateNodeDto(
        Guid SourceId,
        Guid? ParentSourceId,
        string NodeType,
        string Title,
        int Order,
        string? Notes,
        string? MetadataJson,
        Guid? LinkedSectionId);

    public sealed record OutlineTemplateCreateRequest(
        string Name,
        IReadOnlyList<OutlineTemplateNodeDto> Nodes);

    public sealed record OutlineTemplateApplyOptionsDto(
        Guid? ParentNodeId,
        bool CreateLinkedSections,
        string? LinkStrategy);
}
