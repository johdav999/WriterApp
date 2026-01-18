using System;
using System.Collections.Generic;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Commands;
using WriterApp.Domain.Documents;

namespace WriterApp.AI.Actions
{
    public sealed class StoryCoachAction : IAiAction
    {
        public const string ActionIdValue = "synopsis.story_coach";

        public string ActionId => ActionIdValue;

        public string DisplayName => "Story Coach";

        public AiModality[] Modalities => new[] { AiModality.Text };

        public bool RequiresSelection => false;

        public AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string fieldKey = GetInputValue(input.Options, "focus_field_key");
            string focusPrompt = GetInputValue(input.Options, "focus_field_prompt");
            string otherContext = GetInputValue(input.Options, "other_fields_context");
            string existingValue = GetInputValue(input.Options, "existing_value");
            string userNotes = GetInputValue(input.Options, "user_notes");

            TextRange range = new(0, existingValue.Length);
            AiRequestContext context = new(
                input.Document.DocumentId,
                input.ActiveSectionId,
                range,
                existingValue,
                string.IsNullOrWhiteSpace(input.Document.Metadata.Title) ? null : input.Document.Metadata.Title,
                null,
                null,
                input.Document.Metadata.Language,
                existingValue,
                0,
                existingValue.Length,
                null,
                null,
                null);

            Dictionary<string, object> inputs = new()
            {
                ["focus_field_key"] = fieldKey,
                ["focus_field_prompt"] = focusPrompt,
                ["other_fields_context"] = otherContext,
                ["existing_value"] = existingValue,
                ["user_notes"] = userNotes
            };

            return new AiRequest(
                Guid.NewGuid(),
                ActionId,
                Modalities,
                context,
                inputs,
                new Dictionary<string, object>(),
                new Dictionary<string, object>());
        }

        private static string GetInputValue(Dictionary<string, object?>? inputs, string key)
        {
            if (inputs is null || !inputs.TryGetValue(key, out object? value) || value is null)
            {
                return string.Empty;
            }

            return value.ToString() ?? string.Empty;
        }
    }
}
