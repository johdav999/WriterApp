using System;
using WriterApp.Application.Synopsis;
using WriterApp.Domain.Documents;
using SynopsisModel = WriterApp.Domain.Documents.Synopsis;

namespace WriterApp.Application.Commands
{
    public sealed class UpdateSynopsisFieldManualCommand : DocumentEditCommand
    {
        private readonly string _fieldKey;
        private readonly string _newValue;
        private readonly string _oldValue;
        private DateTime _previousModifiedUtc;
        private bool _hasExecuted;

        public UpdateSynopsisFieldManualCommand(Guid provenanceSectionId, string fieldKey, string newValue, string oldValue)
            : base(provenanceSectionId, EditOrigin.User)
        {
            _fieldKey = fieldKey ?? string.Empty;
            _newValue = newValue ?? string.Empty;
            _oldValue = oldValue ?? string.Empty;
        }

        public override string Name => "UpdateSynopsisFieldManual";

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
