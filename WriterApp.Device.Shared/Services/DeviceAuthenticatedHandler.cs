using System.Net;
using System.Net.Http.Headers;

namespace WriterApp.Device.Shared.Services;

public sealed class DeviceAuthenticatedHandler(DeviceAccountService account, Uri backend) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Uri? target = request.RequestUri;
        if (target is null || !target.IsAbsoluteUri || target.Scheme != backend.Scheme
            || target.Host != backend.Host || target.Port != backend.Port
            || !string.IsNullOrEmpty(target.UserInfo)
            || (target.Scheme != "https" && !(target.Scheme == "http" && target.IsLoopback))
            || !target.AbsolutePath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Authenticated requests must target the configured backend API over HTTPS (or local loopback for development).");

        DeviceAccessToken token = await account.GetTokenAsync(cancellationToken);
        long generation = token.Generation;
        if (account.Generation != generation) throw new DeviceSignInRequiredException();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        // Never replay a mutation automatically after a 401, and never open a browser from a request.
        if (response.StatusCode == HttpStatusCode.Unauthorized) account.RejectSession(generation);
        return response;
    }
}
