using System;
using System.Collections.Generic;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;

namespace WriterApp.AI.Actions
{
    public sealed class RewriteSelectionAction : IAiAction
    {
        public const string ActionIdValue = "rewrite.selection";

        public string ActionId => ActionIdValue;

        public string DisplayName => "Rewrite selection";

        public AiModality[] Modalities => new[] { AiModality.Text };

        public bool RequiresSelection => true;

        public AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string sectionPlainText = ResolveSectionText(input.Document, input.ActiveSectionId);
            TextRange normalizedRange = NormalizeRange(input.SelectionRange, sectionPlainText.Length);
            string selectionText = ExtractRange(sectionPlainText, normalizedRange);

            string? languageHint = string.IsNullOrWhiteSpace(input.Document.Metadata.Language)
                ? "en"
                : input.Document.Metadata.Language;

            AiRequestContext context = new(
                input.Document.DocumentId,
                input.ActiveSectionId,
                normalizedRange,
                selectionText,
                string.IsNullOrWhiteSpace(input.Document.Metadata.Title) ? null : input.Document.Metadata.Title,
                null,
                null,
                languageHint,
                selectionText,
                normalizedRange.Start,
                normalizedRange.Length,
                ExtractContainingParagraph(sectionPlainText, normalizedRange),
                ExtractSurroundingBefore(sectionPlainText, normalizedRange),
                ExtractSurroundingAfter(sectionPlainText, normalizedRange));

            Dictionary<string, object> inputs = new()
            {
                ["instruction"] = input.Instruction ?? string.Empty,
                ["tone"] = GetOption(input.Options, "tone", "Neutral"),
                ["length"] = GetOption(input.Options, "length", "Same"),
                ["preserve_terms"] = GetOption(input.Options, "preserve_terms", true)
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

        private static string ResolveSectionText(Document document, Guid sectionId)
        {
            for (int chapterIndex = 0; chapterIndex < document.Chapters.Count; chapterIndex++)
            {
                Chapter chapter = document.Chapters[chapterIndex];
                for (int sectionIndex = 0; sectionIndex < chapter.Sections.Count; sectionIndex++)
                {
                    Section section = chapter.Sections[sectionIndex];
                    if (section.SectionId == sectionId)
                    {
                        return PlainTextMapper.ToPlainText(section.Content.Value);
                    }
                }
            }

            return string.Empty;
        }

        private static TextRange NormalizeRange(TextRange range, int maxLength)
        {
            int start = Math.Clamp(range.Start, 0, maxLength);
            int end = Math.Clamp(range.Start + range.Length, 0, maxLength);
            if (end < start)
            {
                (start, end) = (end, start);
            }

            return new TextRange(start, Math.Max(0, end - start));
        }

        private static string ExtractRange(string text, TextRange range)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            int start = Math.Clamp(range.Start, 0, text.Length);
            int end = Math.Clamp(range.Start + range.Length, 0, text.Length);
            if (end < start)
            {
                (start, end) = (end, start);
            }

            return text.Substring(start, Math.Max(0, end - start));
        }

        private static string? ExtractContainingParagraph(string text, TextRange range)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            int start = Math.Clamp(range.Start, 0, text.Length);
            int end = Math.Clamp(range.Start + range.Length, 0, text.Length);
            if (end < start)
            {
                (start, end) = (end, start);
            }

            int paragraphStart = FindParagraphBoundary(text, start, searchBackward: true);
            int paragraphEnd = FindParagraphBoundary(text, end, searchBackward: false);
            if (paragraphEnd < paragraphStart)
            {
                return null;
            }

            string paragraph = text.Substring(paragraphStart, paragraphEnd - paragraphStart).Trim();
            return string.IsNullOrWhiteSpace(paragraph) ? null : paragraph;
        }

        private static int FindParagraphBoundary(string text, int index, bool searchBackward)
        {
            if (searchBackward)
            {
                int position = Math.Clamp(index, 0, text.Length);
                for (int i = position; i > 0; i--)
                {
                    if (IsParagraphBreak(text, i))
                    {
                        return i;
                    }
                }

                return 0;
            }

            for (int i = index; i < text.Length; i++)
            {
                if (IsParagraphBreak(text, i))
                {
                    return i;
                }
            }

            return text.Length;
        }

        private static bool IsParagraphBreak(string text, int index)
        {
            if (index <= 0 || index >= text.Length)
            {
                return false;
            }

            char current = text[index];
            char previous = text[index - 1];
            if (current == '\n' && previous == '\n')
            {
                return true;
            }

            if (current == '\r' && previous == '\r')
            {
                return true;
            }

            if (previous == '\n' && current == '\r')
            {
                return true;
            }

            return false;
        }

        private static string? ExtractSurroundingBefore(string text, TextRange range)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            const int maxChars = 500;
            int start = Math.Clamp(range.Start, 0, text.Length);
            int beforeStart = Math.Max(0, start - maxChars);
            int length = start - beforeStart;
            if (length <= 0)
            {
                return null;
            }

            return text.Substring(beforeStart, length);
        }

        private static string? ExtractSurroundingAfter(string text, TextRange range)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            const int maxChars = 500;
            int end = Math.Clamp(range.Start + range.Length, 0, text.Length);
            int afterLength = Math.Min(maxChars, text.Length - end);
            if (afterLength <= 0)
            {
                return null;
            }

            return text.Substring(end, afterLength);
        }

        private static string GetOption(Dictionary<string, object?>? options, string key, string defaultValue)
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return defaultValue;
            }

            return value.ToString() ?? defaultValue;
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
