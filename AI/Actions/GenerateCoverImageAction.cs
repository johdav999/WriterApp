using System;
using System.Collections.Generic;
using WriterApp.AI.Abstractions;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;

namespace WriterApp.AI.Actions
{
    public sealed class GenerateCoverImageAction : IAiAction
    {
        public const string ActionIdValue = "generate.image.cover";

        public string ActionId => ActionIdValue;

        public string DisplayName => "Generate cover image";

        public AiModality[] Modalities => new[] { AiModality.Image };

        public bool RequiresSelection => false;

        public AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string prompt = BuildPrompt(input.Document);
            string? languageHint = string.IsNullOrWhiteSpace(input.Document.Metadata.Language)
                ? "en"
                : input.Document.Metadata.Language;

            AiRequestContext context = new(
                input.Document.DocumentId,
                input.ActiveSectionId,
                input.SelectionRange,
                input.SelectedText ?? string.Empty,
                string.IsNullOrWhiteSpace(input.Document.Metadata.Title) ? null : input.Document.Metadata.Title,
                null,
                null,
                languageHint,
                input.SelectedText,
                input.SelectionRange.Start,
                input.SelectionRange.Length,
                null,
                null,
                null);

            Dictionary<string, object> inputs = new()
            {
                ["prompt"] = prompt,
                ["instruction"] = input.Instruction ?? string.Empty
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

        private static string BuildPrompt(Document document)
        {
            if (document is null)
            {
                return string.Empty;
            }

            string title = document.Metadata.Title ?? string.Empty;
            string excerpt = string.Empty;

            for (int chapterIndex = 0; chapterIndex < document.Chapters.Count; chapterIndex++)
            {
                Chapter chapter = document.Chapters[chapterIndex];
                if (chapter.Sections.Count == 0)
                {
                    continue;
                }

                Section section = chapter.Sections[0];
                excerpt = PlainTextMapper.ToPlainText(section.Content.Value);
                if (!string.IsNullOrWhiteSpace(excerpt))
                {
                    break;
                }
            }

            if (excerpt.Length > 120)
            {
                excerpt = excerpt.Substring(0, 120).Trim();
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return excerpt;
            }

            if (string.IsNullOrWhiteSpace(excerpt))
            {
                return title;
            }

            return $"{title} - {excerpt}";
        }
    }
}
