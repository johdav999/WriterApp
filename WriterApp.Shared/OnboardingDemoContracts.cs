namespace WriterApp.Shared;

public sealed record OnboardingDemoWorkspace(Guid ProjectId, Guid DocumentId, Guid SectionId, Guid SceneNodeId);
public sealed record OnboardingDemoStatus(int Version, string Revision, bool HasCompletedOnboarding, int OnboardingStep,
    OnboardingDemoWorkspace? Workspace, bool Available, string Reason, string ActionKey, string Scope,
    bool RequestUsed, Guid? ProposalId, DateTimeOffset? ExpiresAtUtc, bool StructuredSections);
public sealed record OnboardingDemoBootstrapRequest(int Version, Guid OperationId, string PrimaryWritingIntent);
public sealed record OnboardingDemoProgressRequest(int Version, Guid OperationId, string ExpectedRevision, int Step, bool Complete);
