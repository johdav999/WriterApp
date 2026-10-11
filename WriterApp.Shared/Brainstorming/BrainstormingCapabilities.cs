using System.Net;
using System.Text.Json;

namespace WriterApp.Shared.Brainstorming;

public interface IBrainstormingCapabilityService
{
    Task<BrainstormingCapabilities> ReadAsync(CancellationToken ct = default);
}

public enum BrainstormingCapabilityState { Available, NotImplemented, UnsupportedServer, UnsupportedPlatform, AuthenticationRequired, Offline, Unavailable }
public sealed record BrainstormingCapability(BrainstormingCapabilityState State, string Message)
{
    public bool IsAvailable => State == BrainstormingCapabilityState.Available;
}
public sealed record BrainstormingCapabilities(int Version, BrainstormingCapability Sessions,
    BrainstormingCapability Conversation, BrainstormingCapability Streaming, BrainstormingCapability ManuscriptApplication,
    BrainstormingCapability PlanningApplication, BrainstormingCapability Dictation, BrainstormingCapability SpokenDialogue,
    BrainstormingCapability? ContextPreparation = null)
{
    public static BrainstormingCapabilities Current => All(BrainstormingCapabilityState.NotImplemented,
        "Brainstorming speech is not implemented on this backend yet. Continue using text brainstorming.") with {
            Sessions = new(BrainstormingCapabilityState.Available, "Owned version-1 conversation storage is available. Sync explicitly; manuscript application and AI generation are separate capabilities."),
            Conversation = new(BrainstormingCapabilityState.Available, "Checked authenticated conversation transport is available; provider, entitlement and quota checks run per request."),
            Streaming = new(BrainstormingCapabilityState.Available, "Ordered authenticated conversation events and durable inspection are available. Incomplete output cannot become a proposal."),
            ManuscriptApplication = new(BrainstormingCapabilityState.Available, "Explicit checked Keep review, durable application inspection and guarded Undo/Redo are available."),
            PlanningApplication = new(BrainstormingCapabilityState.Available, "Reviewed authored scene ideas, story/character notes and synopsis fields with durable inspection and guarded Undo are available."),
            ContextPreparation = new(BrainstormingCapabilityState.Available, "Checked targets and bounded story-source review are available. Provider generation and manuscript application remain separate capabilities.") };
    public static BrainstormingCapabilities All(BrainstormingCapabilityState state, string message)
    { var c = new BrainstormingCapability(state, message); return new(1, c, c, c, c, c, c, c, c); }
    public void Validate()
    {
        if (Version != 1) throw new InvalidDataException("Unsupported brainstorming capability schema. Update Prosa and the backend.");
        foreach (var c in new[] { Sessions, Conversation, Streaming, ManuscriptApplication, PlanningApplication, Dictation, SpokenDialogue })
            if (c is null || !Enum.IsDefined(c.State) || string.IsNullOrWhiteSpace(c.Message) || c.Message.Length > 2_000)
                throw new InvalidDataException("Invalid brainstorming capability response.");
        if (ContextPreparation is { } context && (!Enum.IsDefined(context.State) || string.IsNullOrWhiteSpace(context.Message) || context.Message.Length > 2000)) throw new InvalidDataException("Invalid story-context capability.");
        if ((Conversation.IsAvailable || ManuscriptApplication.IsAvailable || PlanningApplication.IsAvailable) && !Sessions.IsAvailable
            || Streaming.IsAvailable && !Conversation.IsAvailable || Dictation.IsAvailable && !Conversation.IsAvailable
            || SpokenDialogue.IsAvailable && (!Dictation.IsAvailable || !Streaming.IsAvailable))
            throw new InvalidDataException("Inconsistent brainstorming capabilities.");
    }
    public BrainstormingCapabilities ForPlatform(bool dictationSupported, bool spokenDialogueSupported) => this with
    {
        Dictation = dictationSupported ? Dictation : new(BrainstormingCapabilityState.UnsupportedPlatform,
            "Microphone dictation is unavailable on this platform. Type in the conversation composer."),
        SpokenDialogue = spokenDialogueSupported ? SpokenDialogue : new(BrainstormingCapabilityState.UnsupportedPlatform,
            "Spoken dialogue is unavailable on this platform. Continue with text brainstorming when the backend supports it.")
    };
}

/// <summary>Read-only discovery over each host's existing authenticated HTTP stack. No action fallback or capability cache.</summary>
public static class BrainstormingCapabilityTransport
{
    public const string Route = "api/brainstorming/capabilities";
    public const int MaxResponseBytes = 32_768;
    public static async Task<BrainstormingCapabilities> ReadAsync(HttpClient http, HttpRequestMessage request,
        CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            ct.ThrowIfCancellationRequested();
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented)
                return BrainstormingCapabilities.All(BrainstormingCapabilityState.UnsupportedServer,
                    "This backend does not support brainstorming. Update it, or continue using the existing writing tools.");
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return BrainstormingCapabilities.All(BrainstormingCapabilityState.AuthenticationRequired,
                    "Sign in with an authorized account to check brainstorming availability.");
            if (!response.IsSuccessStatusCode) return Unavailable();
            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var bytes = new MemoryStream(); var block = new byte[4_096]; int count;
            while ((count = await input.ReadAsync(block, timeout.Token)) > 0)
            {
                if (bytes.Length + count > MaxResponseBytes) throw new InvalidDataException("Oversized capabilities.");
                bytes.Write(block, 0, count);
            }
            var value = JsonSerializer.Deserialize<BrainstormingCapabilities>(bytes.ToArray(), BrainstormingValidation.Json)
                ?? throw new InvalidDataException("Missing capabilities.");
            value.Validate(); ct.ThrowIfCancellationRequested(); return value;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Unavailable(); }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidDataException or JsonException)
        { ct.ThrowIfCancellationRequested(); return Unavailable(); }
    }
    private static BrainstormingCapabilities Unavailable() => BrainstormingCapabilities.All(BrainstormingCapabilityState.Unavailable,
        "Brainstorming availability could not be verified. Check the connection and backend version; existing writing tools remain available.");
}
