using System;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    /// <summary>
    /// Test-only AI edit command that wraps a range in <em> tags to validate AI tooling.
    /// </summary>
    public sealed class SimulatedAiEditCommand : DocumentEditCommand, IAiRangeEditCommand
    {
        private readonly AiEditGroup _group;
        private readonly TextRange _range;
        private readonly string? _reason;
        private string? _previousContent;
        private DateTime _previousModifiedUtc;
        private bool _hasExecuted;

        public SimulatedAiEditCommand(Guid sectionId, TextRange range, string? reason = null)
            : base(sectionId, EditOrigin.AI)
        {
            if (range.Start < 0 || range.Length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(range));
            }

            _group = new AiEditGroup(sectionId, reason);
            _range = range;
            _reason = reason;
        }

        public override string Name => "SimulatedAiEdit";

        public Guid AiEditGroupId => _group.GroupId;

        public string? AiEditGroupReason => _group.Reason ?? _reason;

        public TextRange Range => _range;

        public override void Execute(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            (Chapter chapter, int sectionIndex, Section section) = FindSection(document, SectionId);
            string content = section.Content.Value ?? string.Empty;
            if (string.IsNullOrEmpty(content))
            {
                return;
            }

            if (!AiPlainTextRangeMapper.TryMapPlainTextRangeToHtml(content, _range, out int startHtml, out int endHtml))
            {
                startHtml = 0;
                endHtml = content.Length;
            }

            if (startHtml > endHtml)
            {
                (startHtml, endHtml) = (endHtml, startHtml);
            }

            _previousContent = content;
            _previousModifiedUtc = section.ModifiedUtc;

            string updatedContent = content.Insert(endHtml, "</em>").Insert(startHtml, "<em>");
            Section updatedSection = section with
            {
                Content = section.Content with { Value = updatedContent },
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
                Content = section.Content with { Value = _previousContent ?? string.Empty },
                ModifiedUtc = _previousModifiedUtc
            };
            chapter.Sections[sectionIndex] = updatedSection;
        }

    }
}
