using System;

namespace WriterApp.Data.AI
{
    public sealed class PromptPresetRecord
    {
        public Guid Id { get; set; }

        public string OwnerUserId { get; set; } = string.Empty;

        public Guid? ProjectId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Category { get; set; }

        public string Kind { get; set; } = "builtin";

        public string? BuiltinActionId { get; set; }

        public string? TemplateText { get; set; }

        public string ParametersJson { get; set; } = "{}";

        public DateTimeOffset CreatedUtc { get; set; }

        public DateTimeOffset UpdatedUtc { get; set; }
    }
}
