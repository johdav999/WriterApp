using System;
using System.Collections.Generic;
using System.Linq;
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
        private static readonly Regex TokenNameRegex = new(@"^[a-zA-Z0-9_]+$", RegexOptions.Compiled);
        private static readonly Regex AnyCurlyTokenRegex = new(@"\{([^{}]+)\}", RegexOptions.Compiled);
        private static readonly Regex AnyHandlebarsTokenRegex = new(@"\{\{\s*([^{}]+?)\s*\}\}", RegexOptions.Compiled);
        private static readonly Regex AnyJsTokenRegex = new(@"\$\{\s*([^{}]+?)\s*\}", RegexOptions.Compiled);

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

            string normalizedTemplate = NormalizeTemplate(template);
            ValidateTemplate(normalizedTemplate);
            bool strictTokens = GetOptionBool(input.Options, "strictTokens", false);
            Dictionary<string, object?> optionsWithDefaults =
                BuildTemplateOptions(input.Options, sectionText, sourceText, input.SelectionText);
            string expanded = ExpandTemplate(normalizedTemplate, optionsWithDefaults, strictTokens);
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
                ["tone"] = GetOption(optionsWithDefaults, "tone", "Neutral"),
                ["length"] = GetOption(optionsWithDefaults, "length", "Same"),
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

        private static Dictionary<string, object?> BuildTemplateOptions(
            Dictionary<string, object?>? inputOptions,
            string sectionText,
            string sourceText,
            string selectionText)
        {
            Dictionary<string, object?> options = inputOptions is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(inputOptions);

            string? explicitContext = GetOption(options, "context", null);
            if (!string.IsNullOrWhiteSpace(explicitContext))
            {
                return options;
            }

            string? sectionOverride = GetOption(options, "section_text_override", null);
            string? resolvedContext = FirstNonEmpty(
                sectionOverride,
                selectionText,
                sourceText,
                sectionText);

            if (!string.IsNullOrWhiteSpace(resolvedContext))
            {
                options["context"] = resolvedContext;
            }

            return options;
        }

        public static string ExpandTemplate(string template, Dictionary<string, object?>? options, bool strictTokens = false)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            string normalizedTemplate = NormalizeTemplate(template);
            ValidateTemplate(normalizedTemplate);

            if (strictTokens)
            {
                List<string> missingTokens = TokenRegex.Matches(normalizedTemplate)
                    .Select(match => match.Groups[1].Value)
                    .Distinct(StringComparer.Ordinal)
                    .Where(token => GetOption(options, token, null) is null)
                    .Select(token => $"{{{token}}}")
                    .ToList();
                if (missingTokens.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"Template is missing required token value(s): {string.Join(", ", missingTokens)}.");
                }
            }

            return TokenRegex.Replace(normalizedTemplate, match =>
            {
                string key = match.Groups[1].Value;
                string? value = GetOption(options, key, null);
                return value ?? string.Empty;
            });
        }

        public static string NormalizeTemplate(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            string normalized = AnyHandlebarsTokenRegex.Replace(
                template,
                match => "{" + match.Groups[1].Value.Trim() + "}");
            normalized = AnyJsTokenRegex.Replace(
                normalized,
                match => "{" + match.Groups[1].Value.Trim() + "}");
            return normalized;
        }

        public static void ValidateTemplate(string template)
        {
            if (template.Contains("<%", StringComparison.Ordinal)
                || template.Contains("%>", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Template contains unsupported token syntax.");
            }

            List<string> invalidTokens = AnyCurlyTokenRegex.Matches(template)
                .Select(match => match.Groups[1].Value)
                .Where(name => !TokenNameRegex.IsMatch(name))
                .Select(name => $"{{{name}}}")
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (invalidTokens.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Template contains invalid token name(s): {string.Join(", ", invalidTokens)}. Allowed pattern: [a-zA-Z0-9_]+.");
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

        private static bool GetOptionBool(Dictionary<string, object?>? options, string key, bool fallback)
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return fallback;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            if (value is string stringValue && bool.TryParse(stringValue, out bool parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static string? FirstNonEmpty(params string?[] candidates)
        {
            foreach (string? candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
