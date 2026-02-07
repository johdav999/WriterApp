using System;
using System.Collections.Generic;
using System.Text;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Commands;
using WriterApp.Application.Synopsis;

namespace WriterApp.AI.Actions
{
    public sealed class GenerateOutlineFromSynopsisAction : IAiAction
    {
        public const string ActionIdValue = "synopsis.generate_outline";

        public string ActionId => ActionIdValue;

        public string DisplayName => "Generate outline from synopsis";

        public AiModality[] Modalities => new[] { AiModality.Text };

        public bool RequiresSelection => false;

        public AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string mode = GetOption(input.Options, "mode", "chapters");
            if (!string.Equals(mode, "chapters", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(mode, "scenes", StringComparison.OrdinalIgnoreCase))
            {
                mode = "chapters";
            }

            int desiredCount = GetOption(input.Options, "desired_count", 12);
            desiredCount = Math.Clamp(desiredCount, 3, 40);

            string synopsisContext = BuildSynopsisContext(input.Document.Synopsis);
            string instruction = string.IsNullOrWhiteSpace(input.Instruction)
                ? "Generate a structured outline draft from this synopsis."
                : input.Instruction.Trim();

            AiRequestContext context = new(
                input.Document.DocumentId,
                input.ActiveSectionId,
                new TextRange(0, 0),
                string.Empty,
                string.IsNullOrWhiteSpace(input.Document.Metadata.Title) ? null : input.Document.Metadata.Title,
                null,
                null,
                string.IsNullOrWhiteSpace(input.Document.Metadata.Language) ? "en" : input.Document.Metadata.Language,
                null,
                null,
                null,
                null,
                null,
                null);

            Dictionary<string, object> requestInputs = new()
            {
                ["instruction"] = instruction,
                ["mode"] = mode.ToLowerInvariant(),
                ["desired_count"] = desiredCount,
                ["synopsis_context"] = synopsisContext,
                ["document_title"] = input.Document.Metadata.Title ?? string.Empty,
                ["output_contract"] =
                    "Return strict JSON only. Schema: {\"schemaVersion\":\"1.0\",\"mode\":\"chapters|scenes\",\"items\":[{\"index\":1,\"title\":\"...\",\"summary\":\"...\",\"pov\":\"\",\"setting\":\"\",\"beats\":[\"...\"],\"storyRole\":\"\",\"notes\":\"\"}]}"
            };

            return new AiRequest(
                Guid.NewGuid(),
                ActionId,
                Modalities,
                context,
                requestInputs,
                new Dictionary<string, object>(),
                new Dictionary<string, object>());
        }

        private static string BuildSynopsisContext(Domain.Documents.Synopsis synopsis)
        {
            StringBuilder builder = new();
            foreach (SynopsisFieldDefinition field in SynopsisFieldCatalog.Fields)
            {
                if (!SynopsisFieldCatalog.TryGetValue(synopsis, field.Key, out string value))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                builder.Append(field.Label);
                builder.Append(": ");
                builder.AppendLine(value.Trim());
            }

            return builder.ToString().Trim();
        }

        private static string GetOption(Dictionary<string, object?>? options, string key, string defaultValue)
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return defaultValue;
            }

            string text = value.ToString() ?? string.Empty;
            return string.IsNullOrWhiteSpace(text) ? defaultValue : text.Trim();
        }

        private static int GetOption(Dictionary<string, object?>? options, string key, int defaultValue)
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return defaultValue;
            }

            if (value is int number)
            {
                return number;
            }

            return int.TryParse(value.ToString(), out int parsed) ? parsed : defaultValue;
        }
    }
}
