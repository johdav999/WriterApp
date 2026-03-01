using System.Threading;
using System.Threading.Tasks;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Application.Subscriptions
{
    public interface IUserEntitlementStore
    {
        Task<UserEntitlement> GetOrCreateAsync(string userId, CancellationToken cancellationToken = default);
    }
}
