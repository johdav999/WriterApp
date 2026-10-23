namespace WriterApp.Application.Collaboration;

public sealed class CollaborationSessionOptions
{
    public bool SessionsEnabled { get; set; }
    public bool CommitsEnabled { get; set; }
    public string Environment { get; set; } = "";
    public string WebSocketUrl { get; set; } = "";
    public string ServiceKey { get; set; } = "";
    public const int LifetimeSeconds = 300;

    public bool IsConfigured => SessionsEnabled && Environment.Length is > 0 and <= 64
        && Environment.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
        && ServiceKey.Length >= 32 && Uri.TryCreate(WebSocketUrl, UriKind.Absolute, out var url)
        && (url.Scheme == "wss" || (url.Scheme == "ws" && url.IsLoopback))
        && string.IsNullOrEmpty(url.UserInfo) && string.IsNullOrEmpty(url.Query) && string.IsNullOrEmpty(url.Fragment);
}

public sealed record CollaborationSessionRequest(Guid Generation, Guid InstallationId, int ContentContractVersion, int ProtocolVersion, string SchemaFingerprint, bool RequestCommit = false, long MembershipRevision = 0, string? Environment = null);
public sealed record CollaborationSessionTicket(Guid SessionId, long Version, string Ticket, string RoomName, string WebSocketUrl, long ExpiresAtUnixMilliseconds, bool ReadOnly = true);
public sealed record CollaborationSessionRefresh(long Version);
public sealed record CollaborationServiceRequest(string Ticket, string RoomName, Guid ConnectionId, bool Acquire = false);
public sealed record CollaborationBootstrap(string[] Updates, long CommittedSequence, long SnapshotSequence = 0);

public sealed class CollaborationSessionDeniedException : Exception
{
    public CollaborationSessionDeniedException() : base("Collaboration session unavailable. Preserve your local draft.") { }
}
