namespace WriterApp.Device.Shared.Storage;

public sealed record LocalDocumentDraft(
    Guid DocumentId,
    string Title,
    string ContentJson,
    DateTimeOffset UpdatedAtUtc,
    string? ServerVersion = null);
