using System;

namespace WriterApp.Application.Documents
{
    public sealed record PageNotesDto(Guid PageId, string Notes, DateTimeOffset UpdatedAt);

    public sealed record DocumentOutlineDto(Guid DocumentId, string Outline, DateTimeOffset UpdatedAt);
}
