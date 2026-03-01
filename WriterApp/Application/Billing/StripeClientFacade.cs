using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WriterApp.Application.Billing
{
    public sealed class StripeClientFacade : IStripeClientFacade
    {
        private readonly StripeApiClient _stripeApiClient;

        public StripeClientFacade(StripeApiClient stripeApiClient)
        {
            _stripeApiClient = stripeApiClient ?? throw new ArgumentNullException(nameof(stripeApiClient));
        }

        public Task<JsonDocument> GetCheckoutSessionAsync(string apiKey, string sessionId, CancellationToken ct)
        {
            return _stripeApiClient.GetCheckoutSessionAsync(apiKey, sessionId, ct);
        }

        public Task<JsonDocument> GetSubscriptionAsync(string apiKey, string subscriptionId, CancellationToken ct)
        {
            return _stripeApiClient.GetSubscriptionAsync(apiKey, subscriptionId, ct);
        }
    }
}
