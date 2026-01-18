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
                new SynopsisEntry("Premise", synopsis.Premise),
                new SynopsisEntry("Protagonist", synopsis.Protagonist),
                new SynopsisEntry("Antagonist", synopsis.Antagonist),
                new SynopsisEntry("Central Conflict", synopsis.CentralConflict),
                new SynopsisEntry("Theme", synopsis.Theme),
                new SynopsisEntry("Stakes", synopsis.Stakes)
            };

            if (!string.IsNullOrWhiteSpace(synopsis.Resolution))
            {
                entries.Add(new SynopsisEntry("Resolution", synopsis.Resolution));
            }

            return entries;
        }
    }
}
