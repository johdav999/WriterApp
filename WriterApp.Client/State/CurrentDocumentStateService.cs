using System;

namespace WriterApp.Client.State
{
    public sealed class CurrentDocumentStateService
    {
        private Guid? _documentId;
        private Guid? _sectionId;

        public Guid? DocumentId => _documentId;
        public Guid? SectionId => _sectionId;

        public event Action? Changed;

        public void SetCurrent(Guid documentId, Guid sectionId)
        {
            if (documentId == Guid.Empty || sectionId == Guid.Empty)
            {
                Clear();
                return;
            }

            SetState(documentId, sectionId);
        }

        public void SetDocument(Guid documentId)
        {
            if (documentId == Guid.Empty)
            {
                Clear();
                return;
            }

            Guid? nextSectionId = _documentId == documentId ? _sectionId : null;
            SetState(documentId, nextSectionId);
        }

        public void Clear()
        {
            SetState(null, null);
        }

        private void SetState(Guid? documentId, Guid? sectionId)
        {
            if (_documentId == documentId && _sectionId == sectionId)
            {
                return;
            }

            _documentId = documentId;
            _sectionId = sectionId;
            Changed?.Invoke();
        }
    }
}
