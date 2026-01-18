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
            new("premise", "Premise", "What is the core premise?"),
            new("protagonist", "Protagonist", "Who is the main character?"),
            new("antagonist", "Antagonist (optional)", "Who or what opposes the protagonist?"),
            new("central_conflict", "Central Conflict", "What stands in the way?"),
            new("stakes", "Stakes", "What happens if they fail?"),
            new("arc", "Arc", "How does the protagonist change?"),
            new("setting", "Setting", "Where and when does the story unfold?"),
            new("ending", "Ending (optional)", "How does it resolve?")
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
                case "premise":
                    value = synopsis.Premise;
                    return true;
                case "protagonist":
                    value = synopsis.Protagonist;
                    return true;
                case "antagonist":
                    value = synopsis.Antagonist;
                    return true;
                case "central_conflict":
                    value = synopsis.CentralConflict;
                    return true;
                case "stakes":
                    value = synopsis.Stakes;
                    return true;
                case "arc":
                    value = synopsis.Arc;
                    return true;
                case "setting":
                    value = synopsis.Setting;
                    return true;
                case "ending":
                    value = synopsis.Ending;
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
                case "premise":
                    synopsis.Premise = value;
                    return true;
                case "protagonist":
                    synopsis.Protagonist = value;
                    return true;
                case "antagonist":
                    synopsis.Antagonist = value;
                    return true;
                case "central_conflict":
                    synopsis.CentralConflict = value;
                    return true;
                case "stakes":
                    synopsis.Stakes = value;
                    return true;
                case "arc":
                    synopsis.Arc = value;
                    return true;
                case "setting":
                    synopsis.Setting = value;
                    return true;
                case "ending":
                    synopsis.Ending = value;
                    return true;
                default:
                    return false;
            }
        }
    }
}
