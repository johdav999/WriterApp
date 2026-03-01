using System.Threading.Tasks;

namespace WriterApp.Application.Subscriptions
{
    public interface IEntitlementService
    {
        Task<UserEntitlements> GetEntitlementsAsync(string userId);
        Task<bool> HasAsync(string userId, string entitlementKey);
        Task<int?> GetIntAsync(string userId, string entitlementKey);
        void InvalidateForUser(string userId);
    }
}
