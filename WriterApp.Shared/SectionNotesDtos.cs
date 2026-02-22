using System;

namespace WriterApp.Application.Documents
{
    public sealed record SectionNotesDto(Guid SectionId, string NotesText, DateTimeOffset UpdatedAtUtc);
}
