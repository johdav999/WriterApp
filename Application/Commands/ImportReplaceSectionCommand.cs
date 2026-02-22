using System;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    public sealed class ImportReplaceSectionCommand : DocumentEditCommand
    {
        private readonly string _newContent;
        private SectionContent? _previousContent;
        private DateTime _previousModifiedUtc;
        private bool _hasExecuted;

        public ImportReplaceSectionCommand(Guid sectionId, string newContent)
            : base(sectionId, EditOrigin.Import)
        {
            _newContent = newContent ?? string.Empty;
        }

        public override string Name => "ImportReplaceSection";

        public override void Execute(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            (Chapter chapter, int sectionIndex, Section section) = FindSection(document, SectionId);
            _previousContent = section.Content;
            _previousModifiedUtc = section.ModifiedUtc;

            chapter.Sections[sectionIndex] = section with
            {
                Content = section.Content with { Value = _newContent },
                ModifiedUtc = DateTime.UtcNow
            };

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
            chapter.Sections[sectionIndex] = section with
            {
                Content = _previousContent ?? new SectionContent(),
                ModifiedUtc = _previousModifiedUtc
            };
        }
    }
}
