using System;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    /// <summary>
    /// Moves a section to a new order index with reversible state.
    /// </summary>
    public sealed class MoveSectionCommand : IDocumentCommand
    {
        private readonly Guid _sectionId;
        private readonly int _newOrder;
        private int _previousOrder;
        private bool _hasExecuted;

        public MoveSectionCommand(Guid sectionId, int newOrder)
        {
            _sectionId = sectionId;
            _newOrder = newOrder;
        }

        public string Name => "MoveSection";

        public DateTime ExecutedUtc { get; private set; }

        public void Execute(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            (Chapter chapter, int sectionIndex, Section section) = FindSection(document, _sectionId);
            _previousOrder = section.Order;

            Section updatedSection = section with { Order = _newOrder };
            chapter.Sections[sectionIndex] = updatedSection;

            if (ExecutedUtc == default)
            {
                ExecutedUtc = DateTime.UtcNow;
            }

            _hasExecuted = true;
        }

        public void Undo(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (!_hasExecuted)
            {
                throw new InvalidOperationException("Command has not been executed.");
            }

            (Chapter chapter, int sectionIndex, Section section) = FindSection(document, _sectionId);
            Section updatedSection = section with { Order = _previousOrder };
            chapter.Sections[sectionIndex] = updatedSection;
        }

        private static (Chapter Chapter, int SectionIndex, Section Section) FindSection(Document document, Guid sectionId)
        {
            for (int chapterIndex = 0; chapterIndex < document.Chapters.Count; chapterIndex++)
            {
                Chapter chapter = document.Chapters[chapterIndex];
                for (int sectionIndex = 0; sectionIndex < chapter.Sections.Count; sectionIndex++)
                {
                    Section section = chapter.Sections[sectionIndex];
                    if (section.SectionId == sectionId)
                    {
                        return (chapter, sectionIndex, section);
                    }
                }
            }

            throw new InvalidOperationException($"Section {sectionId} was not found.");
        }
    }
}
