using System;

namespace WriterApp.Application.Synopsis
{
    public sealed record DocumentSynopsisDto(
        Guid DocumentId,
        string Logline,
        string Premise,
        string Theme,
        string ProtagonistArc,
        string CentralConflict,
        string Stakes,
        string Setting,
        string EndingIntent,
        string OpenQuestions,
        string Notes,
        DateTimeOffset UpdatedAt,
        WriterApp.Shared.Sync.SyncSynopsis? ExpectedSynopsis = null);

    public sealed record SynopsisAiRequestDto(
        string? FocusFieldKey,
        string? UserNotes,
        int ContractVersion = 0,
        string? ExpectedDocumentVersion = null,
        WriterApp.Shared.Sync.SyncSynopsis? SourceSynopsis = null,
        Guid? ExpectedProjectId = null,
        WriterApp.Shared.WebAiSource? WebSource = null);

    public sealed record SynopsisAiResponseDto(
        string Mode,
        string OutputText,
        string? FocusFieldKey,
        string? ProposedText,
        int ContractVersion = 0,
        Guid ProposalId = default,
        Guid DocumentId = default,
        string? SourceDocumentVersion = null,
        WriterApp.Shared.Sync.SyncSynopsis? SourceSynopsis = null,
        WriterApp.Shared.WebAiSource? WebSource = null,
        DateTimeOffset CreatedUtc = default);
}
