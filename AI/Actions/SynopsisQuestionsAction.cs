using System;
using System.Collections.Generic;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Commands;
using WriterApp.Domain.Documents;

namespace WriterApp.AI.Actions
{
    public sealed class SynopsisQuestionsAction : IAiAction
    {
        public const string ActionIdValue = "synopsis.questions";

        public string ActionId => ActionIdValue;

        public string DisplayName => "Guiding questions";

        public AiModality[] Modalities => new[] { AiModality.Text };

        public bool RequiresSelection => false;

        public AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string synopsisContext = GetInputValue(input.Options, "synopsis_context");
            string userNotes = GetInputValue(input.Options, "user_notes");

            TextRange range = new(0, 0);
            AiRequestContext context = new(
                input.Document.DocumentId,
                input.ActiveSectionId,
                range,
                string.Empty,
                string.IsNullOrWhiteSpace(input.Document.Metadata.Title) ? null : input.Document.Metadata.Title,
                null,
                null,
                input.Document.Metadata.Language,
                string.Empty,
                0,
                0,
                null,
                null,
                null);

            Dictionary<string, object> inputs = new()
            {
                ["synopsis_context"] = synopsisContext,
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
