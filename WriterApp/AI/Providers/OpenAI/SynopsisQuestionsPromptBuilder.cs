using System.Text;

namespace WriterApp.AI.Providers.OpenAI
{
    public static class SynopsisQuestionsPromptBuilder
    {
        private const string EmptyValuePlaceholder = "(not defined yet)";
        private const string EmptyNotesFallback = "No additional input from the author.";

        public static string BuildSystemPrompt(string language)
        {
            return $"You are a story coach. Language: {language}. " +
                   "Ask thoughtful questions that help the author clarify intent and structure. " +
                   "Do not invent plot details or answer the questions.";
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
            prompt.AppendLine("Generate 6-10 guiding questions that help strengthen the synopsis.");
            prompt.AppendLine();
            prompt.AppendLine("Rules:");
            prompt.AppendLine("- Output only the questions, one per line.");
            prompt.AppendLine("- Do NOT include answers.");
            prompt.AppendLine("- Avoid assuming genre or audience; ask if unclear.");
            return prompt.ToString();
        }
    }
}
