using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;

namespace WriterApp.AI.Actions
{
    public sealed class CustomTransformAction : IAiAction
    {
        public const string ActionIdValue = "custom_transform";
        private const int MaxTemplateLength = 2000;
        private static readonly Regex TokenRegex = new(@"\{([a-zA-Z0-9_]+)\}", RegexOptions.Compiled);

        public string ActionId => ActionIdValue;

        public string DisplayName => "Custom transform";

        public AiModality[] Modalities => new[] { AiModality.Text };

        public bool RequiresSelection => false;

        public AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string sectionText = ResolveSectionText(input.Document, input.ActiveSectionId);
            bool scopeSelection = string.Equals(GetOption(input.Options, "scope"), "selection", StringComparison.OrdinalIgnoreCase);
            TextRange range = scopeSelection
                ? NormalizeRange(input.SelectionRange, sectionText.Length)
                : new TextRange(0, sectionText.Length);
            string sourceText = ExtractRange(sectionText, range);

            string template = GetOption(input.Options, "template");
            if (string.IsNullOrWhiteSpace(template))
            {
                throw new InvalidOperationException("Custom template is required.");
            }

            template = template.Trim();
            if (template.Length > MaxTemplateLength)
            {
                throw new InvalidOperationException($"Custom template exceeds {MaxTemplateLength} characters.");
            }

            ValidateTemplate(template);
            string expanded = ExpandTemplate(template, input.Options);
            string instruction =
                $"{expanded}\n\nReturn only revised text. Preserve names, POV, facts, and paragraph breaks. Keep the same language as input. No markdown. No commentary.";

            AiRequestContext context = new(
                input.Document.DocumentId,
                input.ActiveSectionId,
                range,
                sourceText,
                input.Document.Metadata.Title,
                null,
                null,
                string.IsNullOrWhiteSpace(input.Document.Metadata.Language) ? "en" : input.Document.Metadata.Language,
                sourceText,
                range.Start,
                range.Length,
                null,
                null,
                null);

            Dictionary<string, object> inputs = new()
            {
                ["instruction"] = instruction,
                ["tone"] = GetOption(input.Options, "tone", "Neutral"),
                ["length"] = GetOption(input.Options, "length", "Same"),
                ["preserve_terms"] = true
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

        public static string ExpandTemplate(string template, Dictionary<string, object?>? options)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            return TokenRegex.Replace(template, match =>
            {
                string key = match.Groups[1].Value;
                string? value = GetOption(options, key, null);
                return value ?? string.Empty;
            });
        }

        private static void ValidateTemplate(string template)
        {
            if (template.Contains("{{", StringComparison.Ordinal)
                || template.Contains("}}", StringComparison.Ordinal)
                || template.Contains("${", StringComparison.Ordinal)
                || template.Contains("<%", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Template contains unsupported token syntax.");
            }
        }

        private static string ResolveSectionText(Document document, Guid sectionId)
        {
            foreach (Chapter chapter in document.Chapters)
            {
                foreach (Section section in chapter.Sections)
                {
                    if (section.SectionId == sectionId)
                    {
                        return PlainTextMapper.ToPlainText(section.Content.Value ?? string.Empty);
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

        private static string GetOption(Dictionary<string, object?>? options, string key, string fallback = "")
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return fallback;
            }

            return value.ToString() ?? fallback;
        }
    }
}
