using System;

namespace WriterApp.Application.Commands
{
    public sealed record AiEditGroup(Guid GroupId, Guid SectionId, DateTime CreatedUtc, string? Reason)
    {
        public AiEditGroup(Guid sectionId, string? reason = null)
            : this(Guid.NewGuid(), sectionId, DateTime.UtcNow, reason)
        {
        }
    }
}
