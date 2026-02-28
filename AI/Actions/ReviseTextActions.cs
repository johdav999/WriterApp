using System;
using System.Collections.Generic;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;

namespace WriterApp.AI.Actions
{
    public abstract class ReviseTextActionBase : IAiAction
    {
        protected ReviseTextActionBase(string actionId, string displayName, bool requiresSelection, ReviseMode mode)
        {
            ActionIdValue = actionId;
            DisplayNameValue = displayName;
            RequiresSelectionValue = requiresSelection;
            Mode = mode;
        }

        public string ActionId => ActionIdValue;

        public string DisplayName => DisplayNameValue;

        public AiModality[] Modalities => new[] { AiModality.Text };

        public bool RequiresSelection => RequiresSelectionValue;

        protected string ActionIdValue { get; }

        protected string DisplayNameValue { get; }

        protected bool RequiresSelectionValue { get; }

        protected ReviseMode Mode { get; }

        public AiRequest BuildRequest(AiActionInput input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            string sectionPlainText = ResolveSectionText(input.Document, input.ActiveSectionId, input.Options);
            TextRange normalizedRange = RequiresSelection
                ? NormalizeRange(input.SelectionRange, sectionPlainText.Length)
                : new TextRange(0, sectionPlainText.Length);
            string sourceText = RequiresSelection && !string.IsNullOrWhiteSpace(input.SelectedText)
                ? input.SelectedText
                : ExtractRange(sectionPlainText, normalizedRange);
            string tone = GetOption(input.Options, "tone", "Neutral");

            string? languageHint = string.IsNullOrWhiteSpace(input.Document.Metadata.Language)
                ? "en"
                : input.Document.Metadata.Language;

            AiRequestContext context = new(
                input.Document.DocumentId,
                input.ActiveSectionId,
                normalizedRange,
                sourceText,
                string.IsNullOrWhiteSpace(input.Document.Metadata.Title) ? null : input.Document.Metadata.Title,
                null,
                null,
                languageHint,
                sourceText,
                normalizedRange.Start,
                normalizedRange.Length,
                null,
                null,
                null);

            Dictionary<string, object> inputs = new()
            {
                ["instruction"] = BuildInstruction(Mode, tone, RequiresSelection),
                ["tone"] = tone
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

        private static string BuildInstruction(ReviseMode mode, string tone, bool selectionScope)
        {
            string scope = selectionScope ? "selection" : "section";
            string operation = mode switch
            {
                ReviseMode.Tighten => "Tighten the prose for clarity and concision.",
                ReviseMode.Expand => "Expand the prose with useful detail while preserving meaning.",
                ReviseMode.ChangeTone => $"Change the tone to {tone}.",
                ReviseMode.ShowDontTell => "Rewrite telling statements into vivid showing with concrete sensory/action detail.",
                _ => "Revise the text."
            };

            return $"{operation} Return only the revised {scope} text. Preserve names, facts, POV, tense, and paragraph breaks. Keep the same language as the input. No commentary, labels, or markdown.";
        }

        private static string ResolveSectionText(Document document, Guid sectionId, Dictionary<string, object?>? options)
        {
            string? overrideText = GetOption(options, "section_text_override");
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

        private static string GetOption(Dictionary<string, object?>? options, string key, string fallback)
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return fallback;
            }

            return value.ToString() ?? fallback;
        }

        private static string? GetOption(Dictionary<string, object?>? options, string key)
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return null;
            }

            return value.ToString();
        }

        protected enum ReviseMode
        {
            Tighten,
            Expand,
            ChangeTone,
            ShowDontTell
        }
    }

    public sealed class TightenSelectionAction : ReviseTextActionBase
    {
        public new const string ActionIdValue = "tighten.selection";

        public TightenSelectionAction()
            : base(ActionIdValue, "Tighten selection", true, ReviseMode.Tighten)
        {
        }
    }

    public sealed class TightenSectionAction : ReviseTextActionBase
    {
        public new const string ActionIdValue = "tighten.section";

        public TightenSectionAction()
            : base(ActionIdValue, "Tighten section", false, ReviseMode.Tighten)
        {
        }
    }

    public sealed class ExpandSelectionAction : ReviseTextActionBase
    {
        public new const string ActionIdValue = "expand.selection";

        public ExpandSelectionAction()
            : base(ActionIdValue, "Expand selection", true, ReviseMode.Expand)
        {
        }
    }

    public sealed class ExpandSectionAction : ReviseTextActionBase
    {
        public new const string ActionIdValue = "expand.section";

        public ExpandSectionAction()
            : base(ActionIdValue, "Expand section", false, ReviseMode.Expand)
        {
        }
    }

    public sealed class ChangeToneSelectionAction : ReviseTextActionBase
    {
        public new const string ActionIdValue = "change_tone.selection";

        public ChangeToneSelectionAction()
            : base(ActionIdValue, "Change tone (selection)", true, ReviseMode.ChangeTone)
        {
        }
    }

    public sealed class ChangeToneSectionAction : ReviseTextActionBase
    {
        public new const string ActionIdValue = "change_tone.section";

        public ChangeToneSectionAction()
            : base(ActionIdValue, "Change tone (section)", false, ReviseMode.ChangeTone)
        {
        }
    }

    public sealed class ShowDontTellSelectionAction : ReviseTextActionBase
    {
        public new const string ActionIdValue = "show_dont_tell.selection";

        public ShowDontTellSelectionAction()
            : base(ActionIdValue, "Show, don't tell (selection)", true, ReviseMode.ShowDontTell)
        {
        }
    }

    public sealed class ShowDontTellSectionAction : ReviseTextActionBase
    {
        public new const string ActionIdValue = "show_dont_tell.section";

        public ShowDontTellSectionAction()
            : base(ActionIdValue, "Show, don't tell (section)", false, ReviseMode.ShowDontTell)
        {
        }
    }
}
