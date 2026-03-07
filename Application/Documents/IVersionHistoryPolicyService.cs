using System.Threading.Tasks;

namespace WriterApp.Application.Documents
{
    public interface IVersionHistoryPolicyService
    {
        Task<VersionHistoryPolicy> GetPolicyAsync(string userId);
    }
}
