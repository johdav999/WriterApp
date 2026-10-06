namespace WriterApp.UI.Shared;
public sealed record AiHistoryItem(Guid Id, string Action, string Target, string Status, DateTimeOffset CreatedAt,
    string Original, string Proposed, bool CanUndo = false, bool CanRedo = false, string? UnavailableReason = null,
    string Origin = "Local", string? Freshness = null, bool CanRecover = true);
