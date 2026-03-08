using System;
using System.Threading;
using System.Threading.Tasks;

namespace WriterApp.Application.Documents
{
    public interface IProjectDeletionService
    {
        Task<ProjectDeletionResult> DeleteOwnedProjectAsync(Guid incomingId, string ownerUserId, CancellationToken ct);
    }

    public sealed record ProjectDeletionResult(
        bool Deleted,
        Guid? ProjectId,
        ProjectDeletionCounts? Counts);

    public sealed record ProjectDeletionCounts(
        int Documents,
        int Sections,
        int Pages,
        int ProjectNodes,
        int ProjectGoals,
        int ProjectProgressDays,
        int ProjectProgressEvents,
        int ProjectMilestones,
        int WritingSessions,
        int SceneContents,
        int SceneNotes,
        int SceneCards,
        int SceneAnnotations,
        int SceneQualityIssues,
        int SceneVersions,
        int DocumentOutlineNodes,
        int DocumentOutlines,
        int DocumentSynopses,
        int DocumentGlossaryEntries,
        int BibleSnapshots,
        int ProjectExportSettings,
        int PageAnnotations,
        int PageQualityIssues,
        int PageQualityIssueDismissals,
        int PageVersions,
        int PageNotes,
        int SectionNotes,
        int SectionSceneCards,
        int AiActionHistoryEntries,
        int AiActionAppliedEvents,
        int PromptPresets,
        int SearchIndexEntries);
}
