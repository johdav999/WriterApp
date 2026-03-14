using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using WriterApp.Client.State;
using WriterApp.Client.Utilities;

namespace WriterApp.Client.Services
{
    public sealed class ApiUnauthorizedRedirectHandler : DelegatingHandler
    {
        private readonly ILogger<ApiUnauthorizedRedirectHandler> _logger;
        private readonly NavigationManager _navigation;
        private readonly DeletedAccountStateService _deletedAccountStateService;

        public ApiUnauthorizedRedirectHandler(
            ILogger<ApiUnauthorizedRedirectHandler> logger,
            NavigationManager navigation,
            DeletedAccountStateService deletedAccountStateService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _deletedAccountStateService = deletedAccountStateService ?? throw new ArgumentNullException(nameof(deletedAccountStateService));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Forbidden && IsApiRequest(request.RequestUri))
            {
                DeletedAccountApiResponse? deleted = await DeletedAccountApiResponseReader.TryReadAsync(response, cancellationToken);
                if (deleted is not null)
                {
                    _deletedAccountStateService.MarkDeleted(deleted.Message);
                    if (!IsDeletedAccountPath(_navigation.Uri))
                    {
                        _navigation.NavigateTo("/app/deleted-account", replace: true);
                    }

                    return response;
                }
            }

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

        private static bool IsDeletedAccountPath(string uri)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri? absolute))
            {
                return false;
            }

            return absolute.AbsolutePath.Equals("/deleted-account", StringComparison.OrdinalIgnoreCase)
                || absolute.AbsolutePath.Equals("/app/deleted-account", StringComparison.OrdinalIgnoreCase);
        }
    }
}
