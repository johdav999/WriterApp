using System;
using System.Collections.Generic;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Commands;

namespace WriterApp.AI.Actions
{
    public sealed class StoryboardAnalyzePovBalanceAction : IAiAction
    {
        public const string ActionIdValue = "storyboard.analyze-pov-balance";

        public string ActionId => ActionIdValue;

        public string DisplayName => "Analyze POV balance";

        public AiModality[] Modalities => new[] { AiModality.Text };

        public bool RequiresSelection => false;

        public AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string instruction = GetOption(input.Options, "instruction");
            if (string.IsNullOrWhiteSpace(instruction))
            {
                instruction = "Analyze POV balance across the storyboard.";
            }

            string storyboardContext = GetOption(input.Options, "storyboard_context");
            string preferredChapterTitle = GetOption(input.Options, "preferred_chapter_title");

            AiRequestContext context = new(
                input.Document.DocumentId,
                input.ActiveSectionId,
                new TextRange(0, 0),
                string.Empty,
                input.Document.Metadata.Title,
                null,
                preferredChapterTitle,
                input.Document.Metadata.Language,
                null,
                null,
                null,
                null,
                null,
                null);

            Dictionary<string, object> inputs = new()
            {
                ["instruction"] = instruction,
                ["storyboard_context"] = storyboardContext,
                ["preferred_chapter_title"] = preferredChapterTitle
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

        private static string GetOption(Dictionary<string, object?>? options, string key)
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return string.Empty;
            }

            return value.ToString() ?? string.Empty;
        }
    }
}
