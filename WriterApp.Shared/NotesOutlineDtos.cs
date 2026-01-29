using System;

namespace WriterApp.Application.Documents
{
    public sealed record PageNotesDto(Guid PageId, string Notes, DateTimeOffset UpdatedAt);

    public sealed record DocumentOutlineDto(Guid DocumentId, string Outline, DateTimeOffset UpdatedAt);

    public sealed record DocumentOutlineNodeDto(
        Guid Id,
        Guid DocumentId,
        Guid? ParentId,
        int Order,
        string Title,
        string? Notes,
        Guid? LinkedSectionId);

    public sealed record DocumentOutlineLinkRequest(Guid? SectionId);

    public sealed record OutlineApplyOptionsDto(
        bool CreateMissingSections = true,
        bool ReorderSections = true,
        bool RenameSections = false,
        bool LinkNodesToSections = true,
        bool MatchByTitle = true,
        int? MaxDepth = 1);

    public sealed record OutlineApplyResultDto(
        IReadOnlyList<SectionDto> Sections,
        IReadOnlyList<DocumentOutlineNodeDto> Nodes);
}
