using WriterApp.Client.State;

namespace WriterApp.Client.Services;

// Capture manuscript context in the request URL before any asynchronous network work.
public sealed class ManuscriptRequestHandler(ManuscriptSelectionState selection) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.RequestUri is { } uri)
        {
            var parts = uri.AbsolutePath.Trim('/').Split('/');
            if (parts.Length >= 4 && parts[0] == "api" && parts[1] == "projects" && Guid.TryParse(parts[2], out var projectId)
                && parts[3] is "tree" or "nodes" or "stats" or "integrity"
                && !uri.AbsolutePath.EndsWith("/open-scene", StringComparison.OrdinalIgnoreCase)
                && selection.ForProject(projectId) is { } documentId && !uri.Query.Contains("documentId=", StringComparison.OrdinalIgnoreCase))
                request.RequestUri = new Uri(uri + (uri.Query.Length == 0 ? "?" : "&") + $"documentId={documentId:D}");
        }
        return base.SendAsync(request, ct);
    }
}
