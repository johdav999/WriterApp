using System;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    /// <summary>
    /// Updates the content of a section and tracks prior state for undo.
    /// </summary>
    public sealed class UpdateSectionContentCommand : DocumentEditCommand
    {
        private readonly string _newContent;
        private SectionContent? _previousContent;
        private DateTime _previousModifiedUtc;
        private bool _hasExecuted;

        public UpdateSectionContentCommand(Guid sectionId, string newContent)
            : base(sectionId, EditOrigin.User)
        {
            _newContent = newContent ?? string.Empty;
        }

        public override string Name => "UpdateSectionContent";

        public override void Execute(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            (Chapter chapter, int sectionIndex, Section section) = FindSection(document, SectionId);
            _previousContent = section.Content;
            _previousModifiedUtc = section.ModifiedUtc;

            Section updatedSection = section with
            {
                Content = section.Content with { Value = _newContent },
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
                Content = _previousContent ?? new SectionContent(),
                ModifiedUtc = _previousModifiedUtc
            };
            chapter.Sections[sectionIndex] = updatedSection;
        }
    }
}
