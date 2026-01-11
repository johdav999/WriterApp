using System;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    /// <summary>
    /// Applies an AI rewrite result to a section with undoable audit metadata.
    /// </summary>
    public sealed class AiRewriteSectionCommand : IDocumentCommand
    {
        private readonly Guid _sectionId;
        private readonly string _aiResult;
        private SectionContent? _previousContent;
        private DateTime _previousModifiedUtc;
        private SectionAIInfo? _previousAi;
        private bool _hasExecuted;

        public AiRewriteSectionCommand(Guid sectionId, string aiResult)
        {
            _sectionId = sectionId;
            _aiResult = aiResult ?? string.Empty;
        }

        public string Name => "AiRewriteSection";

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
            _previousAi = section.AI;

            Section updatedSection = section with
            {
                Content = section.Content with { Value = _aiResult },
                ModifiedUtc = DateTime.UtcNow,
                AI = section.AI with { LastModifiedByAi = true }
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
                ModifiedUtc = _previousModifiedUtc,
                AI = _previousAi ?? new SectionAIInfo()
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
