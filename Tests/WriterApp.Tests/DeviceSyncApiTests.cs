using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WriterApp.Application.Security;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;

public sealed class DeviceSyncApiTests
{
    [Theory]
    [InlineData(401, false)]
    [InlineData(403, false)]
    [InlineData(422, false)]
    [InlineData(429, true)]
    [InlineData(503, true)]
    public async Task StructuredFailuresPreserveStatusAndRetryClassification(int status, bool retry)
    {
        var api = Api(_ => new((HttpStatusCode)status) { Content = JsonContent.Create(new SyncError("reason", "Safe explanation")) });
        var error = await Assert.ThrowsAsync<DeviceSyncApiException>(() => api.ChangesAsync(null, default));
        Assert.Equal(status, error.Status); Assert.Equal("reason", error.Code); Assert.Equal(retry, error.IsTransient);
    }
    [Fact]
    public async Task HostingSizeRejectionWithoutJsonIsStillPermanent()
    {
        var api = Api(_ => new(HttpStatusCode.RequestEntityTooLarge) { Content = new StringContent("<html>Too large</html>") });
        var error = await Assert.ThrowsAsync<DeviceSyncApiException>(() => api.MutateAsync(Guid.NewGuid(), new(Guid.NewGuid(), null, "trash"), default));
        Assert.Equal(413, error.Status); Assert.False(error.IsTransient);
    }
    [Fact]
    public async Task ProfileRequiresAuthenticatedPaidBackendIdentity()
    {
        var anonymous = Api(_ => new(HttpStatusCode.OK) { Content = JsonContent.Create(new AuthMeDto()) });
        await Assert.ThrowsAsync<DeviceSignInRequiredException>(() => anonymous.GetOwnerAsync(default));
        var free = Api(_ => new(HttpStatusCode.OK) { Content = JsonContent.Create(new AuthMeDto { IsAuthenticated = true, UserId = "owner" }) });
        Assert.Equal(403, (await Assert.ThrowsAsync<DeviceSyncApiException>(() => free.GetOwnerAsync(default))).Status);
        var paid = Api(_ => new(HttpStatusCode.OK) { Content = JsonContent.Create(new AuthMeDto { IsAuthenticated = true, UserId = "owner", IsPaidAccessActive = true }) });
        Assert.Equal("owner", await paid.GetOwnerAsync(default));
    }
    [Fact]
    public async Task OpaqueCursorIsUrlEncodedWithoutChangingItsValue()
    {
        var api = Api(request =>
        {
            Assert.Equal("?limit=100&cursor=a%2Bb%2F%3D%26", request.RequestUri!.Query);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new SyncChanges([], "next", false)) };
        });
        Assert.Equal("next", (await api.ChangesAsync("a+b/=&", default)).Cursor);
    }
    [Fact]
    public async Task MalformedSuccessResponseDoesNotBecomeAnAcknowledgment()
    {
        var api = Api(_ => new(HttpStatusCode.OK) { Content = new StringContent("broken") });
        await Assert.ThrowsAsync<JsonException>(() => api.DownloadAsync(Guid.NewGuid(), default));
    }
    private static DeviceSyncApi Api(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new HttpClient(new Handler(respond)) { BaseAddress = new Uri("https://test.invalid/") });
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
