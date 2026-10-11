using WriterApp.Shared.Brainstorming;

namespace WriterApp.Device.Shared.Services;

public sealed class DeviceBrainstormingCapabilities(HttpClient http, DeviceAccountService account, DeviceConnectivity connectivity,IBrainstormingMicrophone? microphone=null,IBrainstormingSpeechOutput? output=null) : IBrainstormingCapabilityService
{
    public async Task<BrainstormingCapabilities> ReadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        long generation = account.Generation; Uri? backend = http.BaseAddress;
        if (!account.IsSignedIn || string.IsNullOrWhiteSpace(account.AccountId))
            return BrainstormingCapabilities.All(BrainstormingCapabilityState.AuthenticationRequired,
                "Sign in to check brainstorming availability. Local writing remains available.") with {PlanningApplication=new(BrainstormingCapabilityState.Available,"Authored local planning review and save are available offline."),Sessions=new(BrainstormingCapabilityState.Available,"Local session review is available.")};
        if (!connectivity.IsOnline)
            return BrainstormingCapabilities.All(BrainstormingCapabilityState.Offline,
                "Brainstorming generation needs an online backend. Local writing remains available.") with {PlanningApplication=new(BrainstormingCapabilityState.Available,"Authored local planning review and save are available offline."),Sessions=new(BrainstormingCapabilityState.Available,"Local session review is available.")};
        using var request = new HttpRequestMessage(HttpMethod.Get, BrainstormingCapabilityTransport.Route);
        request.Options.Set(DeviceAuthenticatedHandler.ExpectedAccountGeneration, generation);
        BrainstormingCapabilities value;
        try { value = await BrainstormingCapabilityTransport.ReadAsync(http, request, ct); }
        catch (DeviceSignInRequiredException)
        { value = BrainstormingCapabilities.All(BrainstormingCapabilityState.AuthenticationRequired, "Sign in again to check brainstorming availability."); }
        catch (DeviceIdentityUnavailableException)
        { value = BrainstormingCapabilities.All(BrainstormingCapabilityState.Unavailable, "Device sign-in is unavailable. Local writing remains available."); }
        ct.ThrowIfCancellationRequested();
        if (generation != account.Generation || backend != http.BaseAddress)
            throw new OperationCanceledException("The brainstorming account or backend changed.", ct);
        var supported=microphone is not null && (await microphone.AvailabilityAsync(ct)).IsAvailable;
        if(generation != account.Generation || backend != http.BaseAddress)throw new OperationCanceledException("The dictation account changed.");
        var playback=supported && output is not null && ((await output.AvailabilityAsync("en",ct)).IsAvailable || (await output.AvailabilityAsync("sv",ct)).IsAvailable);
        if(generation != account.Generation || backend != http.BaseAddress)throw new OperationCanceledException("The voice account changed.");
        return value.ForPlatform(supported, playback);
    }
}
