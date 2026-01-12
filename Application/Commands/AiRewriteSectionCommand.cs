using System;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    /// <summary>
    /// Applies an AI rewrite result to a section with undoable audit metadata.
    /// </summary>
    public sealed class AiRewriteSectionCommand : DocumentEditCommand
    {
        private readonly string _aiResult;
        private SectionContent? _previousContent;
        private DateTime _previousModifiedUtc;
        private SectionAIInfo? _previousAi;
        private bool _hasExecuted;

        public AiRewriteSectionCommand(Guid sectionId, string aiResult)
            : base(sectionId, EditOrigin.AI)
        {
            _aiResult = aiResult ?? string.Empty;
        }

        public override string Name => "AiRewriteSection";

        public override void Execute(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            (Chapter chapter, int sectionIndex, Section section) = FindSection(document, SectionId);
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
                ModifiedUtc = _previousModifiedUtc,
                AI = _previousAi ?? new SectionAIInfo()
            };
            chapter.Sections[sectionIndex] = updatedSection;
        }
    }
}
