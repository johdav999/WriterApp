using System;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    /// <summary>
    /// Updates the content of a section and tracks prior state for undo.
    /// </summary>
    public sealed class UpdateSectionContentCommand : IDocumentCommand
    {
        private readonly Guid _sectionId;
        private readonly string _newContent;
        private SectionContent? _previousContent;
        private DateTime _previousModifiedUtc;
        private bool _hasExecuted;

        public UpdateSectionContentCommand(Guid sectionId, string newContent)
        {
            _sectionId = sectionId;
            _newContent = newContent ?? string.Empty;
        }

        public string Name => "UpdateSectionContent";

        public DateTime ExecutedUtc { get; private set; }

        public void Execute(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            (Chapter chapter, int sectionIndex, Section section) = FindSection(document, _sectionId);
            _previousContent = section.Content;
            _previousModifiedUtc = section.ModifiedUtc;

            Section updatedSection = section with
            {
                Content = section.Content with { Value = _newContent },
                ModifiedUtc = DateTime.UtcNow
            };
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
            Section updatedSection = section with
            {
                Content = _previousContent ?? new SectionContent(),
                ModifiedUtc = _previousModifiedUtc
            };
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
