using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;

namespace WriterApp.Client.State
{
    public sealed class ProjectProgressCacheService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);
        private readonly ILogger<ProjectProgressCacheService> _logger;
        private readonly Dictionary<Guid, CacheEntry> _entriesByProjectId = new();

        public ProjectProgressCacheService(ILogger<ProjectProgressCacheService> logger)
        {
            _logger = logger;
        }

        public bool TryGetFresh(Guid projectId, out ProjectProgressDashboardDto progress)
        {
            if (projectId != Guid.Empty && _entriesByProjectId.TryGetValue(projectId, out CacheEntry? entry))
            {
                if (DateTimeOffset.UtcNow - entry.CachedAtUtc <= CacheTtl)
                {
                    _logger.LogDebug(
                        "ProjectProgressCache hit ProjectId={ProjectId} AgeSeconds={AgeSeconds}",
                        projectId,
                        Math.Max(0, (int)(DateTimeOffset.UtcNow - entry.CachedAtUtc).TotalSeconds));
                    progress = entry.Progress;
                    return true;
                }

                _entriesByProjectId.Remove(projectId);
                _logger.LogDebug("ProjectProgressCache stale ProjectId={ProjectId}", projectId);
            }

            _logger.LogDebug("ProjectProgressCache miss ProjectId={ProjectId}", projectId);
            progress = default!;
            return false;
        }

        public void Set(Guid projectId, ProjectProgressDashboardDto progress)
        {
            if (projectId == Guid.Empty || progress is null)
            {
                return;
            }

            _entriesByProjectId[projectId] = new CacheEntry(progress, DateTimeOffset.UtcNow);
            _logger.LogDebug("ProjectProgressCache stored ProjectId={ProjectId}", projectId);
        }

        public void InvalidateProject(Guid projectId, string reason = "unspecified")
        {
            if (projectId == Guid.Empty)
            {
                return;
            }

            bool removed = _entriesByProjectId.Remove(projectId);
            _logger.LogDebug(
                "ProjectProgressCache invalidated ProjectId={ProjectId} Reason={Reason} Removed={Removed}",
                projectId,
                reason,
                removed);
        }

        private sealed record CacheEntry(ProjectProgressDashboardDto Progress, DateTimeOffset CachedAtUtc);
    }
}
