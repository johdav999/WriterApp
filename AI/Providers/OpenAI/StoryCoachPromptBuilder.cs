using System.Text;

namespace WriterApp.AI.Providers.OpenAI
{
    public static class StoryCoachPromptBuilder
    {
        private const string EmptyValuePlaceholder = "(not defined yet)";
        private const string EmptyNotesFallback = "No additional input. Generate a proposal based on the synopsis context.";

        public static string BuildSystemPrompt()
        {
            return "You are a professional story editor assisting an author. You may use the synopsis context to reason internally, but your response must ONLY contain text suitable for the focused field. Do not mention or describe other synopsis fields explicitly.";
        }

        public static string BuildUserPrompt(
            string otherFieldsContext,
            string focusFieldKey,
            string focusFieldPrompt,
            string existingValue,
            string userNotes)
        {
            StringBuilder prompt = new();
            prompt.AppendLine("Synopsis Context (unlabeled):");
            if (!string.IsNullOrWhiteSpace(otherFieldsContext))
            {
                prompt.AppendLine(otherFieldsContext.TrimEnd());
            }
            else
            {
                prompt.AppendLine(EmptyValuePlaceholder);
            }

            prompt.AppendLine();
            prompt.AppendLine($"Focus field: {focusFieldKey}");
            prompt.AppendLine($"Focus prompt: {focusFieldPrompt}");
            prompt.AppendLine("Current value:");
            prompt.AppendLine(string.IsNullOrWhiteSpace(existingValue) ? EmptyValuePlaceholder : existingValue);

            prompt.AppendLine();
            prompt.AppendLine("User input:");
            prompt.AppendLine(string.IsNullOrWhiteSpace(userNotes) ? EmptyNotesFallback : userNotes);

            prompt.AppendLine();
            prompt.AppendLine("Task:");
            prompt.AppendLine("Propose a revised version of the focused field only.");
            prompt.AppendLine();
            prompt.AppendLine("Rules:");
            prompt.AppendLine("- Output ONLY the proposed text");
            prompt.AppendLine("- Do NOT include headings or labels");
            prompt.AppendLine("- Do NOT reference other fields explicitly");
            prompt.AppendLine("- Maintain tonal and thematic consistency");
            return prompt.ToString();
        }
    }
}
