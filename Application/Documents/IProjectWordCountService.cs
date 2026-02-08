using System;
using System.Threading;
using System.Threading.Tasks;

namespace WriterApp.Application.Documents
{
    public interface IProjectWordCountService
    {
        Task RefreshProjectAsync(Guid projectId, CancellationToken ct);

        Task RefreshForSectionAsync(Guid sectionId, CancellationToken ct);

        Task<ProjectStatsDto?> GetProjectStatsAsync(string ownerUserId, Guid projectId, CancellationToken ct);
    }
}
