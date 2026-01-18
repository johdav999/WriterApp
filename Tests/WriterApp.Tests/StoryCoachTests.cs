using System;
using WriterApp.AI.Providers.OpenAI;
using WriterApp.Application.AI.StoryCoach;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class StoryCoachTests
    {
        [Fact]
        public void ContextBuilder_UsesUnlabeledOtherFields()
        {
            Synopsis synopsis = new()
            {
                Premise = "A heist in space.",
                Protagonist = "A disgraced pilot.",
                CentralConflict = "A chase with interstellar police."
            };

            StoryCoachContextBuilder builder = new();
            StoryCoachContext context = builder.Build(synopsis, "central_conflict");

            Assert.Contains("A heist in space.", context.OtherFieldsContext, StringComparison.Ordinal);
            Assert.Contains("A disgraced pilot.", context.OtherFieldsContext, StringComparison.Ordinal);
            Assert.DoesNotContain("Premise:", context.OtherFieldsContext, StringComparison.Ordinal);
            Assert.DoesNotContain("Protagonist:", context.OtherFieldsContext, StringComparison.Ordinal);
        }

        [Fact]
        public void PromptBuilder_UsesFallbackWhenUserNotesMissing()
        {
            string prompt = StoryCoachPromptBuilder.BuildUserPrompt(
                "A seed context.",
                "central_conflict",
                "What stands in the way?",
                "Current conflict",
                string.Empty);

            Assert.Contains("No additional input. Generate a proposal based on the synopsis context.", prompt, StringComparison.Ordinal);
        }

        [Fact]
        public void PromptBuilder_IncludesOtherFieldsContext()
        {
            Synopsis synopsis = DocumentFactory.CreateNewDocument().Synopsis;
            synopsis.Protagonist = "A retired detective.";

            StoryCoachContextBuilder builder = new();
            StoryCoachContext context = builder.Build(synopsis, "central_conflict");

            string prompt = StoryCoachPromptBuilder.BuildUserPrompt(
                context.OtherFieldsContext,
                context.FocusFieldKey,
                context.FocusFieldPrompt,
                synopsis.CentralConflict,
                "Focus on the past case.");

            Assert.Contains("A retired detective.", prompt, StringComparison.Ordinal);
            Assert.Contains("Focus field: central_conflict", prompt, StringComparison.Ordinal);
        }
    }
}
