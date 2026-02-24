using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WriterApp.Client.Services
{
    public sealed class ApiUnauthorizedRedirectHandler : DelegatingHandler
    {
        private readonly ILogger<ApiUnauthorizedRedirectHandler> _logger;

        public ApiUnauthorizedRedirectHandler(ILogger<ApiUnauthorizedRedirectHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && IsApiRequest(request.RequestUri))
            {
                _logger.LogInformation(
                    "API returned 401. Leaving auth flow to App Service. Path={Path}",
                    request.RequestUri?.ToString() ?? string.Empty);
            }

            return response;
        }

        private static bool IsApiRequest(Uri? requestUri)
        {
            if (requestUri is null)
            {
                return false;
            }

            string path = requestUri.IsAbsoluteUri ? requestUri.AbsolutePath : requestUri.ToString();
            return path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
