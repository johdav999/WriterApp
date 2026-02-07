using System;
using System.Collections.Generic;
using WriterApp.Domain.Documents;
using SynopsisModel = WriterApp.Domain.Documents.Synopsis;

namespace WriterApp.Application.Synopsis
{
    public sealed record SynopsisFieldDefinition(string Key, string Label, string Placeholder);

    public static class SynopsisFieldCatalog
    {
        private static readonly IReadOnlyList<SynopsisFieldDefinition> FieldsValue = new List<SynopsisFieldDefinition>
        {
            new("logline", "Logline", "Summarize the story in one or two sentences."),
            new("premise", "Premise", "What is the story fundamentally about?"),
            new("theme", "Theme (optional)", "What idea or truth does the story explore?"),
            new("protagonist_arc", "Protagonist Arc", "How does the protagonist change?"),
            new("central_conflict", "Central Conflict", "What stands in the way?"),
            new("stakes", "Stakes", "What happens if they fail?"),
            new("setting", "Setting (optional)", "Where and when does the story unfold?"),
            new("ending_intent", "Ending Intent", "What kind of ending is intended?"),
            new("open_questions", "Open Questions (optional)", "What is unresolved or mysterious?"),
            new("notes", "Notes (optional)", "Anything else you want to remember.")
        };

        public static IReadOnlyList<SynopsisFieldDefinition> Fields => FieldsValue;

        public static bool TryGetValue(SynopsisModel synopsis, string fieldKey, out string value)
        {
            if (synopsis is null)
            {
                throw new ArgumentNullException(nameof(synopsis));
            }

            switch (fieldKey)
            {
                case "logline":
                    value = synopsis.Logline;
                    return true;
                case "premise":
                    value = synopsis.Premise;
                    return true;
                case "central_conflict":
                    value = synopsis.CentralConflict;
                    return true;
                case "theme":
                    value = synopsis.Theme;
                    return true;
                case "stakes":
                    value = synopsis.Stakes;
                    return true;
                case "protagonist_arc":
                    value = synopsis.ProtagonistArc;
                    return true;
                case "setting":
                    value = synopsis.Setting;
                    return true;
                case "ending_intent":
                    value = synopsis.EndingIntent;
                    return true;
                case "open_questions":
                    value = synopsis.OpenQuestions;
                    return true;
                case "notes":
                    value = synopsis.Notes;
                    return true;
                case "outline_draft":
                    value = synopsis.OutlineDraft;
                    return true;
                default:
                    value = string.Empty;
                    return false;
            }
        }

        public static bool TrySetValue(SynopsisModel synopsis, string fieldKey, string value)
        {
            if (synopsis is null)
            {
                throw new ArgumentNullException(nameof(synopsis));
            }

            switch (fieldKey)
            {
                case "logline":
                    synopsis.Logline = value;
                    return true;
                case "premise":
                    synopsis.Premise = value;
                    return true;
                case "central_conflict":
                    synopsis.CentralConflict = value;
                    return true;
                case "theme":
                    synopsis.Theme = value;
                    return true;
                case "stakes":
                    synopsis.Stakes = value;
                    return true;
                case "protagonist_arc":
                    synopsis.ProtagonistArc = value;
                    return true;
                case "setting":
                    synopsis.Setting = value;
                    return true;
                case "ending_intent":
                    synopsis.EndingIntent = value;
                    return true;
                case "open_questions":
                    synopsis.OpenQuestions = value;
                    return true;
                case "notes":
                    synopsis.Notes = value;
                    return true;
                case "outline_draft":
                    synopsis.OutlineDraft = value;
                    return true;
                default:
                    return false;
            }
        }
    }
}
