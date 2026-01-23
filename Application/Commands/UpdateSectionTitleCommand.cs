using System;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    public sealed class UpdateSectionTitleCommand : DocumentEditCommand
    {
        private readonly string _newTitle;
        private string _previousTitle = string.Empty;
        private DateTime _previousModifiedUtc;
        private bool _hasExecuted;

        public UpdateSectionTitleCommand(Guid sectionId, string newTitle)
            : base(sectionId, EditOrigin.User)
        {
            _newTitle = newTitle ?? string.Empty;
        }

        public override string Name => "UpdateSectionTitle";

        public override void Execute(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            (Chapter chapter, int sectionIndex, Section section) = FindSection(document, SectionId);
            _previousTitle = section.Title ?? string.Empty;
            _previousModifiedUtc = section.ModifiedUtc;

            Section updatedSection = section with
            {
                Title = _newTitle,
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
                Title = _previousTitle,
                ModifiedUtc = _previousModifiedUtc
            };

            chapter.Sections[sectionIndex] = updatedSection;
        }
    }
}
