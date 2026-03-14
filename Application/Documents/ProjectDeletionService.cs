using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents
{
    public sealed class ProjectDeletionService : IProjectDeletionService
    {
        private const int DeleteBatchSize = 256;

        private readonly AppDbContext _dbContext;
        private readonly ILogger<ProjectDeletionService> _logger;

        public ProjectDeletionService(
            AppDbContext dbContext,
            ILogger<ProjectDeletionService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<ProjectDeletionResult> DeleteOwnedProjectAsync(Guid incomingId, string ownerUserId, CancellationToken ct)
        {
            if (incomingId == Guid.Empty)
            {
                return Task.FromResult(new ProjectDeletionResult(false, null, null));
            }

            return _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(ct);
                ProjectDeletionResult result = await DeleteOwnedProjectCoreAsync(incomingId, ownerUserId, ct);
                await transaction.CommitAsync(ct);
                return result;
            });
        }

        public Task<ProjectDeletionResult> DeleteOwnedProjectInExistingTransactionAsync(Guid incomingId, string ownerUserId, CancellationToken ct)
        {
            if (incomingId == Guid.Empty)
            {
                return Task.FromResult(new ProjectDeletionResult(false, null, null));
            }

            return DeleteOwnedProjectCoreAsync(incomingId, ownerUserId, ct);
        }

        private async Task<ProjectDeletionResult> DeleteOwnedProjectCoreAsync(Guid incomingId, string ownerUserId, CancellationToken ct)
        {
            ProjectRecord? project = await ResolveOwnedProjectAsync(incomingId, ownerUserId, ct);
            if (project is null)
            {
                _logger.LogWarning(
                    "Project deletion not found. IncomingId={IncomingId} OwnerUserId={OwnerUserId}",
                    incomingId,
                    ownerUserId);
                return new ProjectDeletionResult(false, null, null);
            }

            Guid projectId = project.Id;
            List<Guid> documentIds = await _dbContext.Documents
                .AsNoTracking()
                .Where(item => item.ProjectId == projectId && item.OwnerUserId == ownerUserId)
                .Select(item => item.Id)
                .ToListAsync(ct);

            List<Guid> sectionIds = documentIds.Count == 0
                ? new List<Guid>()
                : await _dbContext.Sections
                    .AsNoTracking()
                    .Where(item => documentIds.Contains(item.DocumentId))
                    .Select(item => item.Id)
                    .ToListAsync(ct);

            List<Guid> pageIds = documentIds.Count == 0
                ? new List<Guid>()
                : await _dbContext.Pages
                    .AsNoTracking()
                    .Where(item => documentIds.Contains(item.DocumentId))
                    .Select(item => item.Id)
                    .ToListAsync(ct);

            List<Guid> projectNodeIds = await _dbContext.ProjectNodes
                .AsNoTracking()
                .Where(item => item.ProjectId == projectId)
                .Select(item => item.Id)
                .ToListAsync(ct);

            List<Guid> historyEntryIds = await _dbContext.AiActionHistoryEntries
                .AsNoTracking()
                .Where(item => item.OwnerUserId == ownerUserId
                    && ((item.DocumentId.HasValue && documentIds.Contains(item.DocumentId.Value))
                        || (item.SectionId.HasValue && sectionIds.Contains(item.SectionId.Value))
                        || (item.PageId.HasValue && pageIds.Contains(item.PageId.Value))))
                .Select(item => item.Id)
                .ToListAsync(ct);

            ProjectDeletionCounts counts = await BuildCountsAsync(
                projectId,
                ownerUserId,
                documentIds,
                sectionIds,
                pageIds,
                projectNodeIds,
                historyEntryIds,
                ct);

            _logger.LogInformation(
                "Project deletion starting. ProjectId={ProjectId} IncomingId={IncomingId} OwnerUserId={OwnerUserId} Documents={Documents} Sections={Sections} Pages={Pages} ProjectNodes={ProjectNodes} SearchIndexEntries={SearchIndexEntries}",
                projectId,
                incomingId,
                ownerUserId,
                counts.Documents,
                counts.Sections,
                counts.Pages,
                counts.ProjectNodes,
                counts.SearchIndexEntries);

            // SQL Server migrations flatten most FKs to Restrict, so we explicitly delete the
            // owned graph in dependency order instead of assuming runtime cascade behavior.
            await DeleteHistoryAsync(ownerUserId, documentIds, sectionIds, pageIds, historyEntryIds, ct);
            await DeleteSearchIndexAsync(projectId, documentIds, ct);
            await DeleteProjectScopedSettingsAsync(projectId, ownerUserId, ct);
            await DeleteSceneChildrenAsync(projectNodeIds, ct);
            await DeleteProjectChildrenAsync(projectId, ct);
            await DeleteDocumentOutlineNodeTreeAsync(documentIds, ct);
            await DeleteDocumentAndPageChildrenAsync(documentIds, sectionIds, pageIds, ct);
            await DeleteProjectNodeTreeAsync(projectId, ct);
            await DeleteCoreContentAsync(documentIds, sectionIds, pageIds, ct);

            int removedProjects = await _dbContext.Projects
                .Where(item => item.Id == projectId && item.OwnerUserId == ownerUserId)
                .ExecuteDeleteAsync(ct);
            if (removedProjects == 0)
            {
                throw new InvalidOperationException($"Project {projectId} disappeared during deletion.");
            }

            _logger.LogInformation(
                "Project deletion completed. ProjectId={ProjectId} OwnerUserId={OwnerUserId} Documents={Documents} Sections={Sections} Pages={Pages} ProjectNodes={ProjectNodes}",
                projectId,
                ownerUserId,
                counts.Documents,
                counts.Sections,
                counts.Pages,
                counts.ProjectNodes);

            return new ProjectDeletionResult(true, projectId, counts);
        }

        private async Task<ProjectDeletionCounts> BuildCountsAsync(
            Guid projectId,
            string ownerUserId,
            IReadOnlyCollection<Guid> documentIds,
            IReadOnlyCollection<Guid> sectionIds,
            IReadOnlyCollection<Guid> pageIds,
            IReadOnlyCollection<Guid> projectNodeIds,
            IReadOnlyCollection<Guid> historyEntryIds,
            CancellationToken ct)
        {
            string normalizedProjectId = IdNorm.Norm(projectId);
            List<string> normalizedDocumentIds = documentIds.Select(IdNorm.Norm).ToList();

            return new ProjectDeletionCounts(
                Documents: documentIds.Count,
                Sections: sectionIds.Count,
                Pages: pageIds.Count,
                ProjectNodes: projectNodeIds.Count,
                ProjectGoals: await _dbContext.ProjectGoals.CountAsync(item => item.ProjectId == projectId, ct),
                ProjectProgressDays: await _dbContext.ProjectProgressDaily.CountAsync(item => item.ProjectId == projectId, ct),
                ProjectProgressEvents: await _dbContext.ProjectProgressEvents.CountAsync(item => item.ProjectId == projectId, ct),
                ProjectMilestones: await _dbContext.ProjectMilestones.CountAsync(item => item.ProjectId == projectId, ct),
                WritingSessions: await _dbContext.WritingSessions.CountAsync(item => item.ProjectId == projectId, ct),
                SceneContents: await CountByIdsAsync(_dbContext.SceneContents, item => item.SceneNodeId, projectNodeIds, ct),
                SceneNotes: await CountByIdsAsync(_dbContext.SceneNotes, item => item.SceneNodeId, projectNodeIds, ct),
                SceneCards: await CountByIdsAsync(_dbContext.SceneCards, item => item.SceneNodeId, projectNodeIds, ct),
                SceneAnnotations: await CountByIdsAsync(_dbContext.SceneAnnotations, item => item.SceneNodeId, projectNodeIds, ct),
                SceneQualityIssues: await CountByIdsAsync(_dbContext.SceneQualityIssues, item => item.SceneNodeId, projectNodeIds, ct),
                SceneVersions: await CountByIdsAsync(_dbContext.SceneVersions, item => item.SceneNodeId, projectNodeIds, ct),
                DocumentOutlineNodes: await CountByIdsAsync(_dbContext.DocumentOutlineNodes, item => item.DocumentId, documentIds, ct),
                DocumentOutlines: await CountByIdsAsync(_dbContext.DocumentOutlines, item => item.DocumentId, documentIds, ct),
                DocumentSynopses: await CountByIdsAsync(_dbContext.DocumentSynopses, item => item.DocumentId, documentIds, ct),
                DocumentGlossaryEntries: await CountByIdsAsync(_dbContext.DocumentGlossaryEntries, item => item.DocumentId, documentIds, ct),
                BibleSnapshots: await CountByIdsAsync(_dbContext.BibleSnapshots, item => item.DocumentId, documentIds, ct),
                ProjectExportSettings: await CountByIdsAsync(_dbContext.ProjectExportSettings, item => item.DocumentId, documentIds, ct),
                PageAnnotations: await CountByIdsAsync(_dbContext.PageAnnotations, item => item.PageId, pageIds, ct),
                PageQualityIssues: await CountByIdsAsync(_dbContext.PageQualityIssues, item => item.PageId, pageIds, ct),
                PageQualityIssueDismissals: await CountByIdsAsync(_dbContext.PageQualityIssueDismissals, item => item.PageId, pageIds, ct),
                PageVersions: await CountByIdsAsync(_dbContext.PageVersions, item => item.PageId, pageIds, ct),
                PageNotes: await CountByIdsAsync(_dbContext.PageNotes, item => item.PageId, pageIds, ct),
                SectionNotes: await CountByIdsAsync(_dbContext.SectionNotes, item => item.SectionId, sectionIds, ct),
                SectionSceneCards: await CountByIdsAsync(_dbContext.SectionSceneCards, item => item.SectionId, sectionIds, ct),
                AiActionHistoryEntries: historyEntryIds.Count,
                AiActionAppliedEvents: await CountByIdsAsync(_dbContext.AiActionAppliedEvents, item => item.HistoryEntryId, historyEntryIds, ct),
                PromptPresets: await _dbContext.PromptPresets.CountAsync(item => item.OwnerUserId == ownerUserId && item.ProjectId == projectId, ct),
                SearchIndexEntries: await _dbContext.SearchIndexEntries.CountAsync(item =>
                    item.ProjectId == normalizedProjectId
                    || (normalizedDocumentIds.Count > 0 && normalizedDocumentIds.Contains(item.DocumentId)),
                    ct));
        }

        private async Task DeleteHistoryAsync(
            string ownerUserId,
            IReadOnlyCollection<Guid> documentIds,
            IReadOnlyCollection<Guid> sectionIds,
            IReadOnlyCollection<Guid> pageIds,
            IReadOnlyCollection<Guid> historyEntryIds,
            CancellationToken ct)
        {
            int appliedDeleted = historyEntryIds.Count == 0
                ? 0
                : await _dbContext.AiActionAppliedEvents
                    .Where(item => historyEntryIds.Contains(item.HistoryEntryId))
                    .ExecuteDeleteAsync(ct);
            _logger.LogDebug("Project deletion stage complete. Stage={Stage} Deleted={Deleted}", "AiActionAppliedEvents", appliedDeleted);

            int historyDeleted = await _dbContext.AiActionHistoryEntries
                .Where(item => item.OwnerUserId == ownerUserId
                    && ((item.DocumentId.HasValue && documentIds.Contains(item.DocumentId.Value))
                        || (item.SectionId.HasValue && sectionIds.Contains(item.SectionId.Value))
                        || (item.PageId.HasValue && pageIds.Contains(item.PageId.Value))))
                .ExecuteDeleteAsync(ct);
            _logger.LogDebug("Project deletion stage complete. Stage={Stage} Deleted={Deleted}", "AiActionHistoryEntries", historyDeleted);
        }

        private async Task DeleteSearchIndexAsync(Guid projectId, IReadOnlyCollection<Guid> documentIds, CancellationToken ct)
        {
            string normalizedProjectId = IdNorm.Norm(projectId);
            List<string> normalizedDocumentIds = documentIds.Select(IdNorm.Norm).ToList();
            int deleted = await _dbContext.SearchIndexEntries
                .Where(item => item.ProjectId == normalizedProjectId
                    || (normalizedDocumentIds.Count > 0 && normalizedDocumentIds.Contains(item.DocumentId)))
                .ExecuteDeleteAsync(ct);
            _logger.LogDebug("Project deletion stage complete. Stage={Stage} Deleted={Deleted}", "SearchIndexEntries", deleted);
        }

        private async Task DeleteProjectScopedSettingsAsync(Guid projectId, string ownerUserId, CancellationToken ct)
        {
            int promptPresetsDeleted = await _dbContext.PromptPresets
                .Where(item => item.OwnerUserId == ownerUserId && item.ProjectId == projectId)
                .ExecuteDeleteAsync(ct);
            _logger.LogDebug("Project deletion stage complete. Stage={Stage} Deleted={Deleted}", "PromptPresets", promptPresetsDeleted);
        }

        private async Task DeleteSceneChildrenAsync(IReadOnlyCollection<Guid> projectNodeIds, CancellationToken ct)
        {
            await DeleteByIdsAsync(_dbContext.SceneAnnotations, item => item.SceneNodeId, projectNodeIds, "SceneAnnotations", ct);
            await DeleteByIdsAsync(_dbContext.SceneQualityIssues, item => item.SceneNodeId, projectNodeIds, "SceneQualityIssues", ct);
            await DeleteByIdsAsync(_dbContext.SceneVersions, item => item.SceneNodeId, projectNodeIds, "SceneVersions", ct);
            await DeleteByIdsAsync(_dbContext.SceneContents, item => item.SceneNodeId, projectNodeIds, "SceneContents", ct);
            await DeleteByIdsAsync(_dbContext.SceneNotes, item => item.SceneNodeId, projectNodeIds, "SceneNotes", ct);
            await DeleteByIdsAsync(_dbContext.SceneCards, item => item.SceneNodeId, projectNodeIds, "SceneCards", ct);
        }

        private async Task DeleteProjectChildrenAsync(Guid projectId, CancellationToken ct)
        {
            await DeleteByQueryAsync(_dbContext.ProjectGoals.Where(item => item.ProjectId == projectId), "ProjectGoals", ct);
            await DeleteByQueryAsync(_dbContext.ProjectProgressDaily.Where(item => item.ProjectId == projectId), "ProjectProgressDaily", ct);
            await DeleteByQueryAsync(_dbContext.ProjectProgressEvents.Where(item => item.ProjectId == projectId), "ProjectProgressEvents", ct);
            await DeleteByQueryAsync(_dbContext.ProjectMilestones.Where(item => item.ProjectId == projectId), "ProjectMilestones", ct);
            await DeleteByQueryAsync(_dbContext.WritingSessions.Where(item => item.ProjectId == projectId), "WritingSessions", ct);
        }

        private async Task DeleteDocumentAndPageChildrenAsync(
            IReadOnlyCollection<Guid> documentIds,
            IReadOnlyCollection<Guid> sectionIds,
            IReadOnlyCollection<Guid> pageIds,
            CancellationToken ct)
        {
            await DeleteByIdsAsync(_dbContext.PageAnnotations, item => item.PageId, pageIds, "PageAnnotations", ct);
            await DeleteByIdsAsync(_dbContext.PageQualityIssues, item => item.PageId, pageIds, "PageQualityIssues", ct);
            await DeleteByIdsAsync(_dbContext.PageQualityIssueDismissals, item => item.PageId, pageIds, "PageQualityIssueDismissals", ct);
            await DeleteByIdsAsync(_dbContext.PageVersions, item => item.PageId, pageIds, "PageVersions", ct);
            await DeleteByIdsAsync(_dbContext.PageNotes, item => item.PageId, pageIds, "PageNotes", ct);
            await DeleteByIdsAsync(_dbContext.SectionNotes, item => item.SectionId, sectionIds, "SectionNotes", ct);
            await DeleteByIdsAsync(_dbContext.SectionSceneCards, item => item.SectionId, sectionIds, "SectionSceneCards", ct);
            await DeleteByIdsAsync(_dbContext.ProjectExportSettings, item => item.DocumentId, documentIds, "ProjectExportSettings", ct);
            await DeleteByIdsAsync(_dbContext.DocumentOutlines, item => item.DocumentId, documentIds, "DocumentOutlines", ct);
            await DeleteByIdsAsync(_dbContext.DocumentSynopses, item => item.DocumentId, documentIds, "DocumentSynopses", ct);
            await DeleteByIdsAsync(_dbContext.DocumentGlossaryEntries, item => item.DocumentId, documentIds, "DocumentGlossaryEntries", ct);
            await DeleteByIdsAsync(_dbContext.BibleSnapshots, item => item.DocumentId, documentIds, "BibleSnapshots", ct);
        }

        private async Task DeleteCoreContentAsync(
            IReadOnlyCollection<Guid> documentIds,
            IReadOnlyCollection<Guid> sectionIds,
            IReadOnlyCollection<Guid> pageIds,
            CancellationToken ct)
        {
            await DeleteByIdsAsync(_dbContext.Pages, item => item.Id, pageIds, "Pages", ct);
            await DeleteByIdsAsync(_dbContext.Sections, item => item.Id, sectionIds, "Sections", ct);
            await DeleteByIdsAsync(_dbContext.Documents, item => item.Id, documentIds, "Documents", ct);
        }

        private async Task DeleteProjectNodeTreeAsync(Guid projectId, CancellationToken ct)
        {
            int deleted = await DeleteLeafFirstAsync(
                _dbContext.ProjectNodes.Where(item => item.ProjectId == projectId),
                item => item.Id,
                ids => _dbContext.ProjectNodes
                    .Where(child => child.ParentId.HasValue && ids.Contains(child.ParentId.Value))
                    .Select(child => child.ParentId!.Value),
                "ProjectNodes",
                ct);
            _logger.LogDebug("Project deletion stage complete. Stage={Stage} Deleted={Deleted}", "ProjectNodes", deleted);
        }

        private async Task DeleteDocumentOutlineNodeTreeAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken ct)
        {
            if (documentIds.Count == 0)
            {
                return;
            }

            int deleted = await DeleteLeafFirstAsync(
                _dbContext.DocumentOutlineNodes.Where(item => documentIds.Contains(item.DocumentId)),
                item => item.Id,
                ids => _dbContext.DocumentOutlineNodes
                    .Where(child => child.ParentId.HasValue && ids.Contains(child.ParentId.Value))
                    .Select(child => child.ParentId!.Value),
                "DocumentOutlineNodes",
                ct);
            _logger.LogDebug("Project deletion stage complete. Stage={Stage} Deleted={Deleted}", "DocumentOutlineNodes", deleted);
        }

        private async Task<int> DeleteLeafFirstAsync<TEntity>(
            IQueryable<TEntity> ownedQuery,
            Expression<Func<TEntity, Guid>> keySelector,
            Func<List<Guid>, IQueryable<Guid>> parentIdQueryFactory,
            string stageName,
            CancellationToken ct)
            where TEntity : class
        {
            int totalDeleted = 0;
            while (true)
            {
                List<Guid> candidateIds = await ownedQuery
                    .Select(keySelector)
                    .Take(DeleteBatchSize * 4)
                    .ToListAsync(ct);
                if (candidateIds.Count == 0)
                {
                    break;
                }

                List<Guid> parentIdList = await parentIdQueryFactory(candidateIds).ToListAsync(ct);
                HashSet<Guid> parentIds = new(parentIdList);
                List<Guid> leafIds = candidateIds
                    .Where(id => !parentIds.Contains(id))
                    .Take(DeleteBatchSize)
                    .ToList();

                if (leafIds.Count == 0)
                {
                    throw new InvalidOperationException($"Unable to find leaf rows while deleting {stageName}.");
                }

                int deleted = await ownedQuery
                    .Where(BuildContainsPredicate(keySelector, leafIds))
                    .ExecuteDeleteAsync(ct);
                totalDeleted += deleted;
            }

            return totalDeleted;
        }

        private async Task DeleteByIdsAsync<TEntity>(
            IQueryable<TEntity> set,
            Expression<Func<TEntity, Guid>> keySelector,
            IReadOnlyCollection<Guid> ids,
            string stageName,
            CancellationToken ct)
            where TEntity : class
        {
            int deleted = await DeleteByIdsAsync(set, keySelector, ids, ct);
            _logger.LogDebug("Project deletion stage complete. Stage={Stage} Deleted={Deleted}", stageName, deleted);
        }

        private static Task<int> DeleteByIdsAsync<TEntity>(
            IQueryable<TEntity> set,
            Expression<Func<TEntity, Guid>> keySelector,
            IReadOnlyCollection<Guid> ids,
            CancellationToken ct)
            where TEntity : class
        {
            if (ids.Count == 0)
            {
                return Task.FromResult(0);
            }

            return set.Where(BuildContainsPredicate(keySelector, ids)).ExecuteDeleteAsync(ct);
        }

        private async Task DeleteByQueryAsync<TEntity>(IQueryable<TEntity> query, string stageName, CancellationToken ct)
            where TEntity : class
        {
            int deleted = await query.ExecuteDeleteAsync(ct);
            _logger.LogDebug("Project deletion stage complete. Stage={Stage} Deleted={Deleted}", stageName, deleted);
        }

        private static Task<int> CountByIdsAsync<TEntity>(
            IQueryable<TEntity> set,
            Expression<Func<TEntity, Guid>> keySelector,
            IReadOnlyCollection<Guid> ids,
            CancellationToken ct)
            where TEntity : class
        {
            if (ids.Count == 0)
            {
                return Task.FromResult(0);
            }

            return set.Where(BuildContainsPredicate(keySelector, ids)).CountAsync(ct);
        }

        private static Expression<Func<TEntity, bool>> BuildContainsPredicate<TEntity>(
            Expression<Func<TEntity, Guid>> keySelector,
            IReadOnlyCollection<Guid> ids)
        {
            ParameterExpression parameter = keySelector.Parameters[0];
            Expression body = Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Contains),
                new[] { typeof(Guid) },
                Expression.Constant(ids),
                keySelector.Body);
            return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
        }

        private async Task<ProjectRecord?> ResolveOwnedProjectAsync(Guid incomingId, string ownerUserId, CancellationToken ct)
        {
            ProjectRecord? project = await _dbContext.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == incomingId && item.OwnerUserId == ownerUserId, ct);
            if (project is not null)
            {
                return project;
            }

            Guid? projectIdFromDocument = await _dbContext.Documents
                .AsNoTracking()
                .Where(item => item.Id == incomingId && item.OwnerUserId == ownerUserId)
                .Select(item => (Guid?)item.ProjectId)
                .FirstOrDefaultAsync(ct);
            if (projectIdFromDocument.HasValue)
            {
                return await _dbContext.Projects
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == projectIdFromDocument.Value && item.OwnerUserId == ownerUserId, ct);
            }

            Guid? projectIdFromNode = await _dbContext.ProjectNodes
                .AsNoTracking()
                .Where(item => item.Id == incomingId)
                .Join(
                    _dbContext.Projects.AsNoTracking(),
                    node => node.ProjectId,
                    row => row.Id,
                    (node, row) => new { row.Id, row.OwnerUserId })
                .Where(item => item.OwnerUserId == ownerUserId)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(ct);
            if (projectIdFromNode.HasValue)
            {
                return await _dbContext.Projects
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == projectIdFromNode.Value && item.OwnerUserId == ownerUserId, ct);
            }

            Guid? projectIdFromSection = await _dbContext.Sections
                .AsNoTracking()
                .Where(item => item.Id == incomingId)
                .Join(
                    _dbContext.Documents.AsNoTracking(),
                    section => section.DocumentId,
                    document => document.Id,
                    (section, document) => new { document.ProjectId, document.OwnerUserId })
                .Where(item => item.OwnerUserId == ownerUserId)
                .Select(item => (Guid?)item.ProjectId)
                .FirstOrDefaultAsync(ct);
            if (projectIdFromSection.HasValue)
            {
                return await _dbContext.Projects
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == projectIdFromSection.Value && item.OwnerUserId == ownerUserId, ct);
            }

            Guid? projectIdFromPage = await _dbContext.Pages
                .AsNoTracking()
                .Where(item => item.Id == incomingId)
                .Join(
                    _dbContext.Documents.AsNoTracking(),
                    page => page.DocumentId,
                    document => document.Id,
                    (page, document) => new { document.ProjectId, document.OwnerUserId })
                .Where(item => item.OwnerUserId == ownerUserId)
                .Select(item => (Guid?)item.ProjectId)
                .FirstOrDefaultAsync(ct);
            if (projectIdFromPage.HasValue)
            {
                return await _dbContext.Projects
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == projectIdFromPage.Value && item.OwnerUserId == ownerUserId, ct);
            }

            return null;
        }
    }
}
