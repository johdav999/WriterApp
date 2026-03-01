using System.Threading.Tasks;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Application.Subscriptions
{
    public interface IPlanRepository
    {
        Task<Plan?> GetPlanForUserAsync(string userId);
        Task<Plan?> GetPlanByKeyAsync(string planKey);
    }
}
