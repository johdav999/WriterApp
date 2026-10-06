namespace WriterApp.Shared;

// Server-captured raw field values; never reconstructed from provider prose or client intent.
public sealed record WebRecoveryTarget(string Kind, Guid Id, Guid? SectionId, int? OrderIndex,
    IReadOnlyDictionary<string,string?> Before, IReadOnlyDictionary<string,string?> After);
public sealed record WebRecoveryPageIdentity(Guid Id, Guid SectionId, int OrderIndex);
public sealed record WebRecoverySectionIdentity(Guid Id, int OrderIndex);
public sealed record WebRecoverySnapshot(int Version, Guid DocumentId, Guid? ProjectId, IReadOnlyList<WebRecoveryTarget> Targets,
    IReadOnlyList<WebRecoveryPageIdentity>? Pages = null, IReadOnlyList<WebRecoverySectionIdentity>? Sections = null);
public sealed record WebRecoveryItem(Guid ApplicationId, Guid ProposalId, string Action, string TargetKind, string Outcome,
    DateTimeOffset CreatedAt, Guid? SectionId, Guid? SceneId, bool CanUndo, bool CanRedo, string Original, string Proposed, string? UnavailableReason, Guid? PageId = null);
public sealed record WebRecoveryRequest(Guid OperationId, Guid ApplicationId, string Outcome, WebAiSource Source);
public sealed record WebRecoveryReceipt(WebAiHistoryIntent Intent, WebAiHistoryReceipt Receipt);
