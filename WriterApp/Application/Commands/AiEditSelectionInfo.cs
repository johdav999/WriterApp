using System;

namespace WriterApp.Application.Commands
{
    public sealed record AiEditSelectionInfo(bool HasIntersection, Guid? GroupId, bool HasMultipleGroups);
}
