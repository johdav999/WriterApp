using System;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    public sealed class ImportAppendSectionCommand : DocumentEditCommand
    {
        private readonly string _appendContent;
        private SectionContent? _previousContent;
        private DateTime _previousModifiedUtc;
        private bool _hasExecuted;

        public ImportAppendSectionCommand(Guid sectionId, string appendContent)
            : base(sectionId, EditOrigin.Import)
        {
            _appendContent = appendContent ?? string.Empty;
        }

        public override string Name => "ImportAppendSection";

        public override void Execute(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            (Chapter chapter, int sectionIndex, Section section) = FindSection(document, SectionId);
            _previousContent = section.Content;
            _previousModifiedUtc = section.ModifiedUtc;

            string previous = section.Content.Value ?? string.Empty;
            string merged = BuildMerged(previous, _appendContent);
            chapter.Sections[sectionIndex] = section with
            {
                Content = section.Content with { Value = merged },
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

        private static string BuildMerged(string existingHtml, string importedHtml)
        {
            if (string.IsNullOrWhiteSpace(existingHtml))
            {
                return importedHtml ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(importedHtml))
            {
                return existingHtml;
            }

            return $"{existingHtml}<p><br></p>{importedHtml}";
        }
    }
}
