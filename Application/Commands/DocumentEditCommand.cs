using System;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    /// <summary>
    /// Base metadata for section-scoped document edits.
    /// </summary>
    public abstract class DocumentEditCommand : IDocumentCommand
    {
        protected DocumentEditCommand(Guid sectionId, EditOrigin origin)
        {
            CommandId = Guid.NewGuid();
            SectionId = sectionId;
            Origin = origin;
        }

        public Guid CommandId { get; }

        public Guid SectionId { get; }

        public DateTime AppliedUtc { get; private set; }

        public EditOrigin Origin { get; }

        public DateTime ExecutedUtc => AppliedUtc;

        public abstract string Name { get; }

        public abstract void Execute(Document document);

        public abstract void Undo(Document document);

        protected void MarkAppliedUtc()
        {
            if (AppliedUtc == default)
            {
                AppliedUtc = DateTime.UtcNow;
            }
        }

        protected static (Chapter Chapter, int SectionIndex, Section Section) FindSection(Document document, Guid sectionId)
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
