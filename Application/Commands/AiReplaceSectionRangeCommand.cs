using System;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    /// <summary>
    /// Replaces a range in a section with AI-provided text and supports undo/redo.
    /// </summary>
    public sealed class AiReplaceSectionRangeCommand : DocumentEditCommand, IAiRangeEditCommand
    {
        private readonly AiEditGroup _group;
        private readonly int _start;
        private readonly int _length;
        private readonly string _newText;
        private DateTime _previousModifiedUtc;
        private bool _hasExecuted;

        public AiReplaceSectionRangeCommand(Guid sectionId, int start, int length, string newText, string? reason = null)
            : this(sectionId, start, length, newText, new AiEditGroup(sectionId, reason), reason)
        {
        }

        public AiReplaceSectionRangeCommand(Guid sectionId, int start, int length, string newText, AiEditGroup group, string? reason = null)
            : base(sectionId, EditOrigin.AI)
        {
            if (start < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            _group = group ?? throw new ArgumentNullException(nameof(group));
            if (_group.SectionId != sectionId)
            {
                throw new ArgumentException("AI edit group section does not match command section.", nameof(group));
            }

            _start = start;
            _length = length;
            _newText = newText ?? string.Empty;
            Reason = reason;
        }

        public override string Name => "AiReplaceSectionRange";

        public Guid AiEditGroupId => _group.GroupId;

        public string? AiEditGroupReason => _group.Reason ?? Reason;

        public TextRange Range => new(_start, _length);

        public int Start => _start;

        public int Length => _length;

        public string NewText => _newText;

        public string? OldText { get; private set; }

        public string? Reason { get; }

        public override void Execute(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            (Chapter chapter, int sectionIndex, Section section) = FindSection(document, SectionId);
            string content = section.Content.Value ?? string.Empty;
            EnsureRange(content, _start, _length);

            string oldText = content.Substring(_start, _length);
            if (OldText is null)
            {
                OldText = oldText;
            }
            else if (!string.Equals(OldText, oldText, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Section content does not match the expected range.");
            }

            _previousModifiedUtc = section.ModifiedUtc;

            string updatedContent = content.Remove(_start, _length).Insert(_start, _newText);
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
            string content = section.Content.Value ?? string.Empty;
            int newLength = _newText.Length;
            EnsureRange(content, _start, newLength);

            string restoredContent = content.Remove(_start, newLength).Insert(_start, OldText ?? string.Empty);
            Section updatedSection = section with
            {
                Content = section.Content with { Value = restoredContent },
                ModifiedUtc = _previousModifiedUtc
            };
            chapter.Sections[sectionIndex] = updatedSection;
        }

        private static void EnsureRange(string content, int start, int length)
        {
            if (start < 0 || length < 0 || start > content.Length || start + length > content.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
        }
    }
}
