using System;
using System.Collections.Generic;

namespace WriterApp.Application.Documents
{
    public sealed record ProjectDto(
        Guid Id,
        string Title,
        string? Subtitle,
        string? AuthorName,
        string? Language,
        string? Genre,
        DateTimeOffset CreatedUtc,
        DateTimeOffset UpdatedUtc,
        int TotalWordCount);

    public sealed record ProjectCreateRequest(
        string? Title,
        string? Subtitle,
        string? AuthorName,
        string? Language,
        string? Genre,
        string? DefaultExportSettingsJson);

    public sealed record ProjectNodeDto(
        Guid Id,
        Guid ProjectId,
        Guid? ParentId,
        string NodeType,
        string Title,
        int OrderIndex,
        Guid? LinkedSectionId,
        string? MetadataJson,
        int WordCountCache,
        DateTimeOffset UpdatedUtc);

    public sealed record ProjectTreeDto(
        ProjectDto Project,
        IReadOnlyList<ProjectNodeDto> Nodes);

    public sealed record ProjectNodeCreateRequest(
        Guid? ParentId,
        string? NodeType,
        string? Title,
        int? OrderIndex,
        Guid? LinkedSectionId,
        string? MetadataJson);

    public sealed record ProjectNodePatchRequest(
        string? Title,
        Guid? ParentId,
        Guid? LinkedSectionId,
        string? MetadataJson,
        string? NodeType);

    public sealed record ProjectNodeReorderRequest(
        IReadOnlyList<Guid> OrderedChildIds);

    public sealed record ProjectStatsDto(
        Guid ProjectId,
        int TotalWordCount,
        IReadOnlyList<ProjectNodeStatDto> Nodes);

    public sealed record ProjectNodeStatDto(
        Guid NodeId,
        int WordCount);

    public sealed record ProjectGoalDto(
        Guid ProjectId,
        int DailyTargetWords,
        int WeeklyTargetWords,
        string Timezone,
        DateTimeOffset UpdatedUtc);

    public sealed record ProjectGoalUpdateRequest(
        int DailyTargetWords,
        int WeeklyTargetWords,
        string? Timezone);

    public sealed record ProjectMilestoneDto(
        Guid Id,
        Guid ProjectId,
        string Title,
        int? TargetWords,
        Guid? TargetNodeId,
        string Status,
        DateTimeOffset? CompletedUtc,
        DateTimeOffset UpdatedUtc);

    public sealed record ProjectMilestoneCreateRequest(
        string? Title,
        int? TargetWords,
        Guid? TargetNodeId);

    public sealed record ProjectMilestoneUpdateRequest(
        string? Title,
        int? TargetWords,
        Guid? TargetNodeId,
        string? Status);

    public sealed record WritingSessionDto(
        Guid Id,
        Guid ProjectId,
        DateTimeOffset StartedUtc,
        DateTimeOffset? EndedUtc,
        int DurationSeconds,
        int WordsDelta,
        string? Notes,
        bool IsActive);

    public sealed record WritingSessionStopRequest(
        string? Notes);

    public sealed record ProjectProgressDashboardDto(
        Guid ProjectId,
        ProjectGoalDto Goal,
        int TodayWords,
        int ThisWeekWords,
        int StreakCount,
        int TotalWordCount,
        IReadOnlyList<ProjectMilestoneDto> Milestones,
        WritingSessionDto? ActiveSession,
        IReadOnlyList<WritingSessionDto> RecentSessions);
}
