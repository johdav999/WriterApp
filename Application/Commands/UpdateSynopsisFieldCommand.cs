using System;
using WriterApp.Application.Synopsis;
using WriterApp.Domain.Documents;
using SynopsisModel = WriterApp.Domain.Documents.Synopsis;

namespace WriterApp.Application.Commands
{
    public sealed class UpdateSynopsisFieldCommand : DocumentEditCommand, IAiEditCommand
    {
        private readonly AiEditGroup _group;
        private readonly string _fieldKey;
        private readonly string _newValue;
        private readonly string _oldValue;
        private readonly string? _reason;
        private DateTime _previousModifiedUtc;
        private bool _hasExecuted;

        public UpdateSynopsisFieldCommand(
            Guid provenanceSectionId,
            string fieldKey,
            string newValue,
            string oldValue,
            AiEditGroup group,
            string? reason = null)
            : base(provenanceSectionId, EditOrigin.AI)
        {
            _group = group ?? throw new ArgumentNullException(nameof(group));
            if (_group.SectionId != provenanceSectionId)
            {
                throw new ArgumentException("AI edit group section does not match command section.", nameof(group));
            }

            _fieldKey = fieldKey ?? string.Empty;
            _newValue = newValue ?? string.Empty;
            _oldValue = oldValue ?? string.Empty;
            _reason = reason;
        }

        public override string Name => "UpdateSynopsisField";

        public string FieldKey => _fieldKey;

        public string NewValue => _newValue;

        public string OldValue => _oldValue;

        public Guid AiEditGroupId => _group.GroupId;

        public string? AiEditGroupReason => _group.Reason ?? _reason;

        public override void Execute(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            SynopsisModel synopsis = document.Synopsis ?? throw new InvalidOperationException("Synopsis is not initialized.");
            _previousModifiedUtc = synopsis.ModifiedUtc;
            ApplyValue(synopsis, _fieldKey, _newValue);
            synopsis.ModifiedUtc = DateTime.UtcNow;

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

            SynopsisModel synopsis = document.Synopsis ?? throw new InvalidOperationException("Synopsis is not initialized.");
            ApplyValue(synopsis, _fieldKey, _oldValue);
            synopsis.ModifiedUtc = _previousModifiedUtc;
        }

        private static void ApplyValue(SynopsisModel synopsis, string fieldKey, string value)
        {
            if (!SynopsisFieldCatalog.TrySetValue(synopsis, fieldKey, value))
            {
                throw new ArgumentException($"Unknown synopsis field '{fieldKey}'.", nameof(fieldKey));
            }
        }
    }
}
