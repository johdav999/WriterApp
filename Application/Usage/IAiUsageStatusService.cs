using System.Threading.Tasks;

namespace WriterApp.Application.Usage
{
    public interface IAiUsageStatusService
    {
        Task<AiUsageStatus> GetStatusAsync(string userId);
    }
}
