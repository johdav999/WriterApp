using System;

namespace WriterApp.Application.Commands
{
    public sealed record AiEditRangeInfo(TextRange Range, Guid GroupId);
}
