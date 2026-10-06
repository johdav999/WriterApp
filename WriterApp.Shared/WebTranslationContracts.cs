namespace WriterApp.Shared;

public sealed record WebTranslationPage(Guid Id, string Title, int OrderIndex, string Content);
public sealed record WebTranslationSection(Guid Id, string Title, string? NarrativePurpose, int OrderIndex,
    string? LanguageCode, Guid? TranslationGroupId, IReadOnlyList<WebTranslationPage> Pages);
public sealed record WebTranslationSource(int Version, string AccountKey, Guid DocumentId, Guid ProjectId,
    string Title, string? LanguageCode, string DocumentVersion, string Fingerprint, string Scope,
    IReadOnlyList<WebTranslationSection> Sections);
public sealed record WebTranslationApproval(Guid OperationId, Guid ProposalId, WebTranslationSource Source,
    TranslationStructure Original, TranslationStructure Translated, string Mode, string SourceLanguage,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool IsWriting = false);
public sealed record WebTranslationReceipt(Guid OperationId, Guid ProposalId, Guid SourceDocumentId,
    string State, Guid? ResultDocumentId, Guid? ResultSectionId, Guid OriginalCopyId);
public sealed record TranslationPageReview(string Label, string Original, string Proposed);
