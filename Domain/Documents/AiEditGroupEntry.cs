using System;
using System.Collections.Generic;

namespace WriterApp.Domain.Documents
{
    public sealed record AiEditGroupEntry
    {
        public Guid GroupId { get; init; }

        public DateTime AppliedUtc { get; init; }

        public string? Reason { get; init; }

        public List<Guid> CommandIds { get; init; } = new();
    }
}
