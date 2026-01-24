using System;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    /// <summary>
    /// Updates per-section notes and tracks prior state for undo.
    /// </summary>
    public sealed class UpdateSectionNotesCommand : DocumentEditCommand
    {
        private readonly string _newNotes;
        private string _previousNotes = string.Empty;
        private DateTime _previousModifiedUtc;
        private bool _hasExecuted;

        public UpdateSectionNotesCommand(Guid sectionId, string newNotes)
            : base(sectionId, EditOrigin.User)
        {
            _newNotes = newNotes ?? string.Empty;
        }

        public override string Name => "UpdateSectionNotes";

        public override void Execute(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            (Chapter chapter, int sectionIndex, Section section) = FindSection(document, SectionId);
            _previousNotes = section.Notes ?? string.Empty;
            _previousModifiedUtc = section.ModifiedUtc;

            Section updatedSection = section with
            {
                Notes = _newNotes,
                ModifiedUtc = DateTime.UtcNow
            };
            chapter.Sections[sectionIndex] = updatedSection;

            MarkAppliedUtc();

            _hasExecuted = true;
        }

        public override void Undo(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (!_hasExecuted)
            {
                throw new InvalidOperationException("Command has not been executed.");
            }

            (Chapter chapter, int sectionIndex, Section section) = FindSection(document, SectionId);
            Section updatedSection = section with
            {
                Notes = _previousNotes,
                ModifiedUtc = _previousModifiedUtc
            };
            chapter.Sections[sectionIndex] = updatedSection;
        }
    }
}
