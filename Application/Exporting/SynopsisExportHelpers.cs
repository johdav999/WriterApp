using System;
using System.Collections.Generic;
using WriterApp.Domain.Documents;
using SynopsisModel = WriterApp.Domain.Documents.Synopsis;

namespace WriterApp.Application.Exporting
{
    internal static class SynopsisExportHelpers
    {
        public sealed record SynopsisEntry(string Label, string Value);

        public static IReadOnlyList<SynopsisEntry> GetOrderedEntries(SynopsisModel synopsis)
        {
            if (synopsis is null)
            {
                throw new ArgumentNullException(nameof(synopsis));
            }

            List<SynopsisEntry> entries = new()
            {
                new SynopsisEntry("Logline", synopsis.Logline),
                new SynopsisEntry("Premise", synopsis.Premise),
                new SynopsisEntry("Theme", synopsis.Theme),
                new SynopsisEntry("Protagonist Arc", synopsis.ProtagonistArc),
                new SynopsisEntry("Central Conflict", synopsis.CentralConflict),
                new SynopsisEntry("Stakes", synopsis.Stakes),
                new SynopsisEntry("Setting", synopsis.Setting),
                new SynopsisEntry("Ending Intent", synopsis.EndingIntent),
                new SynopsisEntry("Open Questions", synopsis.OpenQuestions),
                new SynopsisEntry("Notes", synopsis.Notes)
            };

            return entries;
        }
    }
}
