using System.Collections.Generic;

namespace WriterApp.Domain.Documents
{
    /// <summary>
    /// AI-related state for a section, used for audit trails and review workflows.
    /// </summary>
    public record SectionAIInfo
    {
        public bool LastModifiedByAi { get; init; }

        public List<AIHistoryEntry> AIHistory { get; init; } = new();

        public SectionAIInfo()
        {
        }
    }
}
