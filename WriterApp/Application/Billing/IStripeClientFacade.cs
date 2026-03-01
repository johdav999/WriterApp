using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WriterApp.Application.Billing
{
    public interface IStripeClientFacade
    {
        Task<JsonDocument> GetCheckoutSessionAsync(string apiKey, string sessionId, CancellationToken ct);
        Task<JsonDocument> GetSubscriptionAsync(string apiKey, string subscriptionId, CancellationToken ct);
    }
}
