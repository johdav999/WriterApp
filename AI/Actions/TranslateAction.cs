using System;
using System.Collections.Generic;
using System.Linq;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;

namespace WriterApp.AI.Actions
{
    public abstract class TranslateActionBase : IAiAction
    {
        protected TranslateActionBase(string actionId, string displayName, bool requiresSelection, TranslateScope scope)
        {
            ActionIdValue = actionId;
            DisplayNameValue = displayName;
            RequiresSelectionValue = requiresSelection;
            Scope = scope;
        }

        public string ActionId => ActionIdValue;

        public string DisplayName => DisplayNameValue;

        public AiModality[] Modalities => new[] { AiModality.Text };

        public bool RequiresSelection => RequiresSelectionValue;

        protected string ActionIdValue { get; }

        protected string DisplayNameValue { get; }

        protected bool RequiresSelectionValue { get; }

        protected TranslateScope Scope { get; }

        public AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string sourceText = ResolveSourceText(
                input.Document,
                input.ActiveSectionId,
                input.SelectionRange,
                input.SelectedText,
                input.Options);
            TextRange range = new(0, sourceText.Length);

            string languageHint = string.IsNullOrWhiteSpace(input.Document.Metadata.Language)
                ? "en"
                : input.Document.Metadata.Language;

            string? selectionText = Scope == TranslateScope.Selection ? sourceText : null;

            AiRequestContext context = new(
                input.Document.DocumentId,
                input.ActiveSectionId,
                range,
                sourceText,
                string.IsNullOrWhiteSpace(input.Document.Metadata.Title) ? null : input.Document.Metadata.Title,
                null,
                null,
                languageHint,
                selectionText,
                range.Start,
                range.Length,
                ExtractContainingParagraph(sourceText, range),
                ExtractSurroundingBefore(sourceText, range),
                ExtractSurroundingAfter(sourceText, range));

            string sourceLanguage = GetOption(input.Options, "source_language", "auto");
            string targetLanguage = GetOption(input.Options, "target_language", "en");
            string style = GetOption(input.Options, "style", "natural");
            string instruction = BuildInstruction(sourceLanguage, targetLanguage, style, Scope);

            Dictionary<string, object> inputs = new()
            {
                ["instruction"] = instruction,
                ["source_language"] = sourceLanguage,
                ["target_language"] = targetLanguage,
                ["style"] = style
            };

            if (Scope == TranslateScope.Document)
            {
                inputs["section_markers"] = "Preserve [[SECTION:{id}]] markers unchanged.";
            }

            return new AiRequest(
                Guid.NewGuid(),
                ActionId,
                Modalities,
                context,
                inputs,
                new Dictionary<string, object>(),
                new Dictionary<string, object>());
        }

        private string ResolveSourceText(
            Document document,
            Guid sectionId,
            TextRange selectionRange,
            string? selectedText,
            Dictionary<string, object?>? options)
        {
            if (Scope == TranslateScope.Document)
            {
                return BuildDocumentText(document);
            }

            string sectionText = ResolveSectionText(document, sectionId, options);
            if (Scope == TranslateScope.Section)
            {
                return sectionText;
            }

            if (!string.IsNullOrWhiteSpace(selectedText))
            {
                return selectedText;
            }

            TextRange normalizedRange = NormalizeRange(selectionRange, sectionText.Length);
            return ExtractRange(sectionText, normalizedRange);
        }

        private static string BuildDocumentText(Document document)
        {
            List<string> chunks = new();
            foreach (Chapter chapter in document.Chapters.OrderBy(ch => ch.Order))
            {
                foreach (Section section in chapter.Sections.OrderBy(sec => sec.Order))
                {
                    string plain = PlainTextMapper.ToPlainText(section.Content.Value);
                    chunks.Add($"[[SECTION:{section.SectionId}]]");
                    chunks.Add(plain);
                    chunks.Add(string.Empty);
                }
            }

            return string.Join(Environment.NewLine, chunks).Trim();
        }

        private static string BuildInstruction(string sourceLanguage, string targetLanguage, string style, TranslateScope scope)
        {
            string scopeLabel = scope == TranslateScope.Document ? "document" : scope == TranslateScope.Section ? "section" : "selection";
            string source = string.IsNullOrWhiteSpace(sourceLanguage) ? "auto" : sourceLanguage;
            string target = string.IsNullOrWhiteSpace(targetLanguage) ? "en" : targetLanguage;
            string tone = string.IsNullOrWhiteSpace(style) ? "natural" : style;
            return $"Translate the {scopeLabel} from {source} to {target}. Style: {tone}. Preserve paragraph breaks and whitespace. Keep any [[SECTION:...]] marker lines unchanged.";
        }

        private static string ResolveSectionText(Document document, Guid sectionId, Dictionary<string, object?>? options)
        {
            string? overrideText = GetOption(options, "section_text_override", null);
            if (!string.IsNullOrWhiteSpace(overrideText))
            {
                return overrideText;
            }

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

            int start = Math.Clamp(range.Start, 0, text.Length);
            int beforeStart = Math.Max(0, start - 240);
            if (beforeStart >= start)
            {
                return null;
            }

            string snippet = text.Substring(beforeStart, start - beforeStart).Trim();
            return string.IsNullOrWhiteSpace(snippet) ? null : snippet;
        }

        private static string? ExtractSurroundingAfter(string text, TextRange range)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            int end = Math.Clamp(range.Start + range.Length, 0, text.Length);
            int afterEnd = Math.Min(text.Length, end + 240);
            if (afterEnd <= end)
            {
                return null;
            }

            string snippet = text.Substring(end, afterEnd - end).Trim();
            return string.IsNullOrWhiteSpace(snippet) ? null : snippet;
        }

        private static string GetOption(Dictionary<string, object?>? options, string key, string? fallback)
        {
            if (options is null)
            {
                return fallback ?? string.Empty;
            }

            if (!options.TryGetValue(key, out object? value))
            {
                return fallback ?? string.Empty;
            }

            return value?.ToString() ?? fallback ?? string.Empty;
        }

        protected enum TranslateScope
        {
            Selection,
            Section,
            Document
        }
    }

    public sealed class TranslateSelectionAction : TranslateActionBase
    {
        public new const string ActionIdValue = "translate.selection";

        public TranslateSelectionAction()
            : base(ActionIdValue, "Translate selection", true, TranslateScope.Selection)
        {
        }
    }

    public sealed class TranslateSectionAction : TranslateActionBase
    {
        public new const string ActionIdValue = "translate.section";

        public TranslateSectionAction()
            : base(ActionIdValue, "Translate section", false, TranslateScope.Section)
        {
        }
    }

    public sealed class TranslateDocumentAction : TranslateActionBase
    {
        public new const string ActionIdValue = "translate.document";

        public TranslateDocumentAction()
            : base(ActionIdValue, "Translate document", false, TranslateScope.Document)
        {
        }
    }
}
