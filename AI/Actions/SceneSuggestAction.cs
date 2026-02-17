using System;
using System.Collections.Generic;
using System.Linq;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;

namespace WriterApp.AI.Actions
{
    public sealed class SceneSuggestAction : IAiAction
    {
        public const string ActionIdValue = "scene.suggest";

        public string ActionId => ActionIdValue;

        public string DisplayName => "Suggest scene card";

        public AiModality[] Modalities => new[] { AiModality.Text };

        public bool RequiresSelection => false;

        public AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            Section? section = ResolveSection(input.Document, input.ActiveSectionId);
            string sectionTitle = section?.Title ?? string.Empty;
            string sectionText = GetOption(input.Options, "section_text_override");
            if (string.IsNullOrWhiteSpace(sectionText))
            {
                sectionText = PlainTextMapper.ToPlainText(section?.Content.Value ?? string.Empty).Trim();
            }
            int maxChars = GetOptionInt(input.Options, "max_section_chars", 0);
            if (maxChars > 0 && sectionText.Length > maxChars)
            {
                sectionText = sectionText.Substring(0, maxChars);
            }
            string instruction = string.IsNullOrWhiteSpace(input.Instruction)
                ? "Suggest scene card fields based on the section content."
                : input.Instruction.Trim();

            AiRequestContext context = new(
                input.Document.DocumentId,
                input.ActiveSectionId,
                new TextRange(0, 0),
                string.Empty,
                input.Document.Metadata.Title,
                null,
                sectionTitle,
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
                ["mode"] = "suggest",
                ["section_title"] = sectionTitle,
                ["section_text"] = sectionText,
                ["narrative_purpose"] = GetOption(input.Options, "narrative_purpose"),
                ["emotional_beat"] = GetOption(input.Options, "emotional_beat"),
                ["key_events"] = GetOption(input.Options, "key_events"),
                ["open_questions"] = GetOption(input.Options, "open_questions"),
                ["pov_character_id"] = GetOption(input.Options, "pov_character_id"),
                ["place_id"] = GetOption(input.Options, "place_id"),
                ["timeline_event_id"] = GetOption(input.Options, "timeline_event_id"),
                ["time_ref"] = GetOption(input.Options, "time_ref"),
                ["tags_json"] = GetOption(input.Options, "tags_json"),
                ["references_json"] = GetOption(input.Options, "references_json")
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

        private static Section? ResolveSection(Document document, Guid sectionId)
        {
            return document.Chapters
                .SelectMany(chapter => chapter.Sections)
                .FirstOrDefault(section => section.SectionId == sectionId);
        }

        private static string GetOption(Dictionary<string, object?>? options, string key)
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return string.Empty;
            }

            return value.ToString() ?? string.Empty;
        }

        private static int GetOptionInt(Dictionary<string, object?>? options, string key, int defaultValue)
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return defaultValue;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            return int.TryParse(value.ToString(), out int parsed) ? parsed : defaultValue;
        }
    }
}
