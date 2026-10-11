namespace WriterApp.Shared.Brainstorming;

// These are inert, versioned data contracts. Validation is not authorization or a write command.
public enum BrainstormingIdentitySpace { Local, Server }
public enum BrainstormingConversationState { LocalOnly, PendingSave, Saved, Conflict, Deleted }
public enum BrainstormingGenerationState { Idle, Prepared, Accepted, Streaming, Completed, Cancelled, Failed, Interrupted, Unknown }
public enum BrainstormingApplicationState { Unreviewed, Approved, Applying, Committed, Cancelled, Failed, Unknown, Undone }
public enum BrainstormingRole { User, Assistant }
public enum BrainstormingDraftOrigin { Ai, AuthorDictated }
public enum BrainstormingTurnState { Unsent, Accepted, Complete, Incomplete, Cancelled, Failed }
public enum BrainstormingIntent { Explore, Clarify, Draft, Revise, ProposePlanning }
public enum BrainstormingTargetKind { InsertInPage, ReplaceRange, CreateSection }
public enum BrainstormingSourceKind { Manuscript, Synopsis, Scene, StoryNote, Character, Place, Timeline, SupportingDocument }
public enum BrainstormingFreshness { Saved, PendingEditorChanges, LocalAheadOfServer, Cached, Stale }
public enum BrainstormingPlanningKind { SceneIdea, StoryNote, SynopsisField, CharacterNote }
public enum BrainstormingEventKind { Accepted, AnswerDelta, DraftPending, Completed, Cancelled, Failed }

/// <summary>IDs are in one explicit namespace. Mapping never changes a session's ownership.</summary>
public sealed record BrainstormingScope(int Version, Guid SessionId, Guid DocumentId, Guid? ProjectId,
    BrainstormingIdentitySpace Space, string BackendKey, string? AccountKey, Guid? LocalOwnerId);
public sealed record BrainstormingMapping(int Version, BrainstormingScope Local, BrainstormingScope Server,
    long Revision, DateTimeOffset UpdatedAt);
public sealed record BrainstormingSourceStamp(string ContentRevision, string StructureRevision, string Fingerprint);

/// <summary>A resolved target uses saved identities and a checked position, never a displayed ordinal.
/// Range offsets use UTF-16 editor text mapping identified by TextMapping; scene-content writes are excluded.</summary>
public sealed record BrainstormingTarget(int Version, Guid Id, Guid DocumentId, BrainstormingTargetKind Kind,
    BrainstormingSourceStamp Source, Guid? ChapterNodeId, Guid? SceneNodeId, Guid? SectionId, Guid? PageId,
    Guid? ParentNodeId, Guid? BeforeSectionId, Guid? BeforeNodeId, int? From, int? To, string? TextMapping);
public sealed record BrainstormingClarification(int Version, Guid RequestId, string Question,
    IReadOnlyList<BrainstormingTarget> Candidates);

// Included entries retain the exact excerpt sent; omitted entries must not contain excluded text.
public sealed record BrainstormingContextPart(Guid SectionId, Guid PageId, string Revision, string ContentFingerprint, int From, int To);
public sealed record BrainstormingContextSource(Guid Id, Guid DocumentId, BrainstormingSourceKind Kind,
    string Title, BrainstormingSourceStamp Source, BrainstormingFreshness Freshness, bool ExplicitlyEnabled,
    bool Included, bool Mandatory, bool IsSummary, string Reason, string Text,
    Guid? SectionId = null, Guid? PageId = null, int? From = null, int? To = null,
    Guid? SummaryOfSourceId = null, string? SummaryOfRevision = null,
    BrainstormingSourceAvailability Availability = BrainstormingSourceAvailability.Available,
    BrainstormingSourceNature Nature = BrainstormingSourceNature.ManuscriptFact,
    IReadOnlyList<BrainstormingContextPart>? Parts = null);
public sealed record BrainstormingContext(int Version, Guid Id, BrainstormingScope Scope, DateTimeOffset CapturedAt,
    IReadOnlyList<BrainstormingContextSource> Sources, BrainstormingContextPolicyInfo? Policy = null);
public sealed record BrainstormingTurn(int Version, Guid Id, BrainstormingScope Scope, long Sequence,
    Guid? ParentTurnId, Guid OperationId, Guid? RequestId, Guid? ContextId, BrainstormingRole Role,
    BrainstormingTurnState State, string Text, DateTimeOffset CreatedAt);
public sealed record BrainstormingRequest(int Version, Guid Id, BrainstormingScope Scope, Guid OperationId,
    Guid UserTurnId, Guid ContextId, Guid? TargetId, Guid? ParentDraftId, long ExpectedSessionRevision,
    BrainstormingIntent Intent, BrainstormingGenerationState State, DateTimeOffset CreatedAt);
public sealed record BrainstormingDraft(int Version, Guid Id, BrainstormingScope Scope, Guid RequestId,
    Guid TurnId, Guid ContextId, Guid TargetId, Guid? ParentDraftId, long Revision, bool ManuallyEdited,
    string Instruction, string PlainText, string Constraints, IReadOnlyList<string> IntroducedFacts, DateTimeOffset CreatedAt,
    string? EditorHtml = null, IReadOnlyList<string>? Contradictions = null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] BrainstormingDraftOrigin Origin = BrainstormingDraftOrigin.Ai,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Guid? DictationOperationId = null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Guid? BranchId = null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? BranchName = null);
public sealed record BrainstormingPlanningProposal(int Version, Guid Id, BrainstormingScope Scope, Guid TurnId,
    Guid ContextId, BrainstormingPlanningKind Kind, string? EntityKey, BrainstormingSourceStamp Source,
    string Field, string Before, string Proposed, long Revision, DateTimeOffset CreatedAt, bool AuthoredDocumentNotes = false);
/// <summary>Approval pins exact edited content and revision; it is not proof of application.</summary>
public sealed record BrainstormingApproval(int Version, Guid Id, BrainstormingScope Scope, Guid OperationId,
    Guid CandidateId, long CandidateRevision, string ReviewedContentFingerprint, DateTimeOffset ReviewedAt);
/// <summary>Only a confirmed durable receipt can be Committed. Unknown requires reconciliation, never blind retry.</summary>
public sealed record BrainstormingApplication(int Version, Guid OperationId, BrainstormingScope Scope,
    Guid ApprovalId, BrainstormingApplicationState State, Guid? ReceiptId, Guid? HistoryOperationId,
    BrainstormingSourceStamp? CommittedSource, string Message, DateTimeOffset UpdatedAt);
public sealed record BrainstormingEvent(int Version, BrainstormingScope Scope, Guid RequestId, Guid OperationId,
    Guid TurnId, long Sequence, BrainstormingEventKind Kind, string Text, Guid? ResultId, DateTimeOffset CreatedAt);
/// <summary>Bounded aggregate for persistence; listing/pagination is a separate transport in prompt 2.</summary>
public sealed record BrainstormingSession(int Version, BrainstormingScope Scope, long Revision,
    BrainstormingConversationState State, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<BrainstormingTurn> Turns, IReadOnlyList<BrainstormingContext> Contexts,
    IReadOnlyList<BrainstormingTarget> Targets, IReadOnlyList<BrainstormingRequest> Requests,
    IReadOnlyList<BrainstormingDraft> Drafts, IReadOnlyList<BrainstormingPlanningProposal> Planning,
    IReadOnlyList<BrainstormingApproval> Approvals, IReadOnlyList<BrainstormingApplication> Applications,
    IReadOnlyList<BrainstormingSourceSelection>? EnabledSources = null, Guid? BranchedFromSessionId = null);
