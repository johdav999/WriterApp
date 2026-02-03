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
        DateTimeOffset UpdatedAt);

    public sealed record SynopsisAiRequestDto(
        string? FocusFieldKey,
        string? UserNotes);

    public sealed record SynopsisAiResponseDto(
        string Mode,
        string OutputText,
        string? FocusFieldKey,
        string? ProposedText);
}
