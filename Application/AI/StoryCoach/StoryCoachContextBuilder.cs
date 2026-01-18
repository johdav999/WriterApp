using System;
using System.Collections.Generic;
using System.Text;
using WriterApp.Application.Synopsis;
using WriterApp.Domain.Documents;
using SynopsisModel = WriterApp.Domain.Documents.Synopsis;

namespace WriterApp.Application.AI.StoryCoach
{
    public sealed record StoryCoachContext(
        string FocusFieldKey,
        string FocusFieldPrompt,
        string OtherFieldsContext);

    public sealed class StoryCoachContextBuilder
    {
        private const string EmptyValuePlaceholder = "(not defined yet)";

        public StoryCoachContext Build(SynopsisModel synopsis, string focusFieldKey)
        {
            if (synopsis is null)
            {
                throw new ArgumentNullException(nameof(synopsis));
            }

            string prompt = GetFocusPrompt(focusFieldKey);
            string otherFields = BuildOtherFieldsContext(synopsis, focusFieldKey);
            return new StoryCoachContext(focusFieldKey, prompt, otherFields);
        }

        private static string BuildOtherFieldsContext(SynopsisModel synopsis, string focusFieldKey)
        {
            List<string> parts = new();

            foreach (SynopsisFieldDefinition field in SynopsisFieldCatalog.Fields)
            {
                if (string.Equals(field.Key, focusFieldKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (SynopsisFieldCatalog.TryGetValue(synopsis, field.Key, out string value)
                    && !string.IsNullOrWhiteSpace(value))
                {
                    parts.Add(value.Trim());
                }
                else
                {
                    parts.Add(EmptyValuePlaceholder);
                }
            }

            StringBuilder builder = new();
            for (int index = 0; index < parts.Count; index++)
            {
                if (index > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine();
                }

                builder.Append(parts[index]);
            }

            return builder.ToString();
        }

        private static string GetFocusPrompt(string fieldKey)
        {
            return fieldKey switch
            {
                "premise" => "What core idea or hook should this story communicate?",
                "protagonist" => "Who carries the emotional weight of this story, and what do they want?",
                "antagonist" => "Who or what opposes the protagonist?",
                "central_conflict" => "What stands between the protagonist and their goal?",
                "theme" => "What theme or message runs through the story?",
                "stakes" => "What will be lost if the protagonist fails?",
                "arc" => "How does the protagonist change from start to finish?",
                "setting" => "Where and when does the story take place?",
                "ending" => "What outcome brings the story to a satisfying close?",
                "resolution" => "What final resolution brings the story to closure?",
                _ => "Share what you want to develop in this field."
            };
        }
    }
}
