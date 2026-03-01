using System;
using System.Collections.Generic;
using System.Linq;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;

namespace WriterApp.AI.Actions
{
    public sealed class ProposeNextParagraphAction : IAiAction
    {
        public const string ActionIdValue = "propose.next-paragraph";
        private const int RecentContextMinChars = 1200;
        private const int RecentContextTargetChars = 2000;
        private const int RecentContextMaxChars = 2500;

        public string ActionId => ActionIdValue;

        public string DisplayName => "Propose next paragraph";

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
            string sectionText = PlainTextMapper.ToPlainText(section?.Content.Value ?? string.Empty).Trim();
            string recentContext = ExtractRecentContext(sectionText);

            string instruction = string.IsNullOrWhiteSpace(input.Instruction)
                ? "Propose the next paragraph for the current section."
                : input.Instruction.Trim();

            string? languageHint = string.IsNullOrWhiteSpace(input.Document.Metadata.Language)
                ? "en"
                : input.Document.Metadata.Language;

            AiRequestContext context = new(
                input.Document.DocumentId,
                input.ActiveSectionId,
                new TextRange(0, 0),
                string.Empty,
                input.Document.Metadata.Title,
                null,
                sectionTitle,
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
                ["section_title"] = sectionTitle,
                ["recent_context"] = recentContext,
                ["narrative_purpose"] = GetOption(input.Options, "narrative_purpose"),
                ["emotional_beat"] = GetOption(input.Options, "emotional_beat"),
                ["key_events"] = GetOption(input.Options, "key_events"),
                ["open_questions"] = GetOption(input.Options, "open_questions")
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

        private static string ExtractRecentContext(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string trimmed = text.Trim();
            if (trimmed.Length <= RecentContextMaxChars)
            {
                return trimmed;
            }

            string[] paragraphs = trimmed
                .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

            if (paragraphs.Length > 1)
            {
                List<string> selected = new();
                int length = 0;

                for (int index = paragraphs.Length - 1; index >= 0; index--)
                {
                    string paragraph = paragraphs[index].Trim();
                    if (paragraph.Length == 0)
                    {
                        continue;
                    }

                    int extra = paragraph.Length + (selected.Count > 0 ? 2 : 0);
                    if (selected.Count > 0 && length + extra > RecentContextMaxChars)
                    {
                        break;
                    }

                    selected.Insert(0, paragraph);
                    length += extra;

                    if (length >= RecentContextTargetChars)
                    {
                        break;
                    }
                }

                string joined = string.Join("\n\n", selected).Trim();
                if (joined.Length >= RecentContextMinChars)
                {
                    return joined;
                }
            }

            int start = Math.Max(0, trimmed.Length - RecentContextMinChars);
            string tail = trimmed.Substring(start).Trim();
            int boundary = tail.IndexOf("\n\n", StringComparison.Ordinal);
            if (boundary > 0 && boundary < tail.Length - 20)
            {
                tail = tail.Substring(boundary + 2).Trim();
            }

            if (tail.Length > RecentContextMaxChars)
            {
                tail = tail.Substring(tail.Length - RecentContextMaxChars).Trim();
            }

            return tail;
        }
    }
}
