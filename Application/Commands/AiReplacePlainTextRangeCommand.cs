using System;
using System.Net;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    public sealed class AiReplacePlainTextRangeCommand : DocumentEditCommand, IAiRangeEditCommand
    {
        private readonly AiEditGroup _group;
        private readonly TextRange _range;
        private readonly string _newText;
        private readonly string? _reason;
        private string? _previousContent;
        private DateTime _previousModifiedUtc;
        private bool _hasExecuted;

        public AiReplacePlainTextRangeCommand(Guid sectionId, TextRange range, string newText, AiEditGroup group, string? reason = null)
            : base(sectionId, EditOrigin.AI)
        {
            if (range.Start < 0 || range.Length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(range));
            }

            _group = group ?? throw new ArgumentNullException(nameof(group));
            if (_group.SectionId != sectionId)
            {
                throw new ArgumentException("AI edit group section does not match command section.", nameof(group));
            }

            _range = range;
            _newText = newText ?? string.Empty;
            _reason = reason;
        }

        public override string Name => "AiReplacePlainTextRange";

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

            int startHtml;
            int endHtml;
            if (!AiPlainTextRangeMapper.TryMapPlainTextRangeToHtml(content, _range, out startHtml, out endHtml))
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

            string encoded = WebUtility.HtmlEncode(_newText) ?? string.Empty;
            string updatedContent = content.Remove(startHtml, Math.Max(0, endHtml - startHtml)).Insert(startHtml, encoded);
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
