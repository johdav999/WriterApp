using System;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    public sealed class UpdateSectionMetadataCommand : DocumentEditCommand
    {
        private readonly SectionKind _kind;
        private readonly bool _includeInNumbering;
        private readonly SectionNumberingStyle _numberingStyle;
        private SectionKind _previousKind;
        private bool _previousIncludeInNumbering;
        private SectionNumberingStyle _previousNumberingStyle;
        private DateTime _previousModifiedUtc;
        private bool _hasExecuted;

        public UpdateSectionMetadataCommand(Guid sectionId, SectionKind kind, bool includeInNumbering, SectionNumberingStyle numberingStyle)
            : base(sectionId, EditOrigin.User)
        {
            _kind = kind;
            _includeInNumbering = includeInNumbering;
            _numberingStyle = numberingStyle;
        }

        public override string Name => "UpdateSectionMetadata";

        public override void Execute(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            (Chapter chapter, int sectionIndex, Section section) = FindSection(document, SectionId);
            _previousKind = section.Kind;
            _previousIncludeInNumbering = section.IncludeInNumbering;
            _previousNumberingStyle = section.NumberingStyle;
            _previousModifiedUtc = section.ModifiedUtc;

            Section updatedSection = section with
            {
                Kind = _kind,
                IncludeInNumbering = _includeInNumbering,
                NumberingStyle = _numberingStyle,
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
                Kind = _previousKind,
                IncludeInNumbering = _previousIncludeInNumbering,
                NumberingStyle = _previousNumberingStyle,
                ModifiedUtc = _previousModifiedUtc
            };

            chapter.Sections[sectionIndex] = updatedSection;
        }
    }
}
