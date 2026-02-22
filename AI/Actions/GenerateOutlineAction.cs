using System;
using System.Collections.Generic;
using System.Linq;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;

namespace WriterApp.AI.Actions
{
    public sealed class GenerateOutlineAction : IAiAction
    {
        public const string ActionIdValue = "generate.outline";

        public string ActionId => ActionIdValue;

        public string DisplayName => "Generate outline";

        public AiModality[] Modalities => new[] { AiModality.Text };

        public bool RequiresSelection => false;

        public AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string? languageHint = string.IsNullOrWhiteSpace(input.Document.Metadata.Language)
                ? "en"
                : input.Document.Metadata.Language;
            string instruction = string.IsNullOrWhiteSpace(input.Instruction)
                ? "Generate a hierarchical outline as JSON."
                : input.Instruction.Trim();

            int maxSectionChars = GetOption(input.Options, "max_section_chars", 2000);
            int maxSections = GetOption(input.Options, "max_sections", 60);
            bool truncated = GetOption(input.Options, "truncated", false);

            List<(string Title, string Content)> sections = BuildSectionSummaries(
                input.Document,
                maxSections,
                maxSectionChars);

            string sectionPayload = string.Join(
                "\n\n",
                sections.Select((entry, index) =>
                    $"Section {index + 1}: {entry.Title}\n{entry.Content}"));

            AiRequestContext context = new(
                input.Document.DocumentId,
                input.ActiveSectionId,
                new TextRange(0, 0),
                string.Empty,
                string.IsNullOrWhiteSpace(input.Document.Metadata.Title) ? null : input.Document.Metadata.Title,
                null,
                null,
                languageHint,
                null,
                null,
                null,
                null,
                null,
                null);

            Dictionary<string, object> inputs = new()
            {
                ["instruction"] = instruction,
                ["document_title"] = input.Document.Metadata.Title ?? string.Empty,
                ["sections"] = sectionPayload,
                ["section_titles"] = sections.Select(entry => entry.Title).ToList(),
                ["truncated"] = truncated
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

        private static List<(string Title, string Content)> BuildSectionSummaries(
            Document document,
            int maxSections,
            int maxChars)
        {
            List<(string Title, string Content)> result = new();
            IEnumerable<Section> sections = document.Chapters
                .SelectMany(chapter => chapter.Sections)
                .OrderBy(section => section.Order);

            foreach (Section section in sections)
            {
                if (result.Count >= maxSections)
                {
                    break;
                }

                string title = string.IsNullOrWhiteSpace(section.Title) ? "Untitled" : section.Title.Trim();
                string plain = PlainTextMapper.ToPlainText(section.Content.Value ?? string.Empty);
                string trimmed = plain.Trim();
                if (maxChars > 0 && trimmed.Length > maxChars)
                {
                    trimmed = trimmed.Substring(0, maxChars);
                }

                result.Add((title, trimmed));
            }

            return result;
        }

        private static int GetOption(Dictionary<string, object?>? options, string key, int defaultValue)
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

        private static bool GetOption(Dictionary<string, object?>? options, string key, bool defaultValue)
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return defaultValue;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            return bool.TryParse(value.ToString(), out bool parsed) ? parsed : defaultValue;
        }
    }
}
