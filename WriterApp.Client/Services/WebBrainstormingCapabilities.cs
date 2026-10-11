using WriterApp.Shared.Brainstorming;

namespace WriterApp.Client.Services;

public sealed class WebBrainstormingCapabilities(HttpClient http, AuthStateService auth,IBrainstormingMicrophone? microphone=null,IBrainstormingSpeechOutput? output=null) : IBrainstormingCapabilityService
{
    public async Task<BrainstormingCapabilities> ReadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); await auth.GetAsync(ct: ct);
        string? account = auth.UserId; Uri? backend = http.BaseAddress;
        if (!auth.IsAuthenticated || string.IsNullOrWhiteSpace(account))
            return BrainstormingCapabilities.All(BrainstormingCapabilityState.AuthenticationRequired, "Sign in to check brainstorming availability.");
        using var request = new HttpRequestMessage(HttpMethod.Get, BrainstormingCapabilityTransport.Route);
        var value = await BrainstormingCapabilityTransport.ReadAsync(http, request, ct);
        ct.ThrowIfCancellationRequested();
        if (!auth.IsAuthenticated || account != auth.UserId || backend != http.BaseAddress)
            throw new OperationCanceledException("The brainstorming account or backend changed.", ct);
        var supported=microphone is not null && (await microphone.AvailabilityAsync(ct)).IsAvailable;
        if (!auth.IsAuthenticated || account != auth.UserId || backend != http.BaseAddress)throw new OperationCanceledException("The dictation account changed.");
        var playback=supported && output is not null && ((await output.AvailabilityAsync("en",ct)).IsAvailable || (await output.AvailabilityAsync("sv",ct)).IsAvailable);
        if (!auth.IsAuthenticated || account != auth.UserId || backend != http.BaseAddress)throw new OperationCanceledException("The voice account changed.");
        return value.ForPlatform(supported, playback);
    }
}
