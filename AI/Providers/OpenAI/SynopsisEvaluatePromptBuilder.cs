using System.Text;

namespace WriterApp.AI.Providers.OpenAI
{
    public static class SynopsisEvaluatePromptBuilder
    {
        private const string EmptyValuePlaceholder = "(not defined yet)";
        private const string EmptyNotesFallback = "No additional input from the author.";

        public static string BuildSystemPrompt(string language)
        {
            return $"You are a story coach. Language: {language}. " +
                   "Evaluate a synopsis without inventing new plot details. " +
                   "Be candid but supportive. Ask for clarification when information is missing.";
        }

        public static string BuildUserPrompt(string synopsisContext, string userNotes)
        {
            StringBuilder prompt = new();
            prompt.AppendLine("Synopsis context:");
            prompt.AppendLine(string.IsNullOrWhiteSpace(synopsisContext) ? EmptyValuePlaceholder : synopsisContext.TrimEnd());
            prompt.AppendLine();
            prompt.AppendLine("Author notes:");
            prompt.AppendLine(string.IsNullOrWhiteSpace(userNotes) ? EmptyNotesFallback : userNotes.TrimEnd());
            prompt.AppendLine();
            prompt.AppendLine("Task:");
            prompt.AppendLine("Evaluate the synopsis and return the following sections:");
            prompt.AppendLine("- Strengths");
            prompt.AppendLine("- Potential weaknesses");
            prompt.AppendLine("- Missing elements");
            prompt.AppendLine("- Clarity issues");
            prompt.AppendLine();
            prompt.AppendLine("Rules:");
            prompt.AppendLine("- Do NOT rewrite the synopsis.");
            prompt.AppendLine("- Do NOT invent plot details.");
            prompt.AppendLine("- Use concise bullet points.");
            return prompt.ToString();
        }
    }
}
