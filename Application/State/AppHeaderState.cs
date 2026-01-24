using System;

namespace WriterApp.Application.State
{
    public sealed class AppHeaderState
    {
        private string? _documentTitle;
        private string? _documentId;
        private bool _canRenameDocumentTitle;

        public event Action? OnChange;
        public event Action<string>? DocumentTitleEdited;

        public string? DocumentTitle
        {
            get => _documentTitle;
            set
            {
                if (string.Equals(_documentTitle, value, StringComparison.Ordinal))
                {
                    return;
                }

                _documentTitle = value;
                OnChange?.Invoke();
            }
        }

        public string? DocumentId
        {
            get => _documentId;
            set
            {
                if (string.Equals(_documentId, value, StringComparison.Ordinal))
                {
                    return;
                }

                _documentId = value;
                OnChange?.Invoke();
            }
        }

        public bool CanRenameDocumentTitle
        {
            get => _canRenameDocumentTitle;
            set
            {
                if (_canRenameDocumentTitle == value)
                {
                    return;
                }

                _canRenameDocumentTitle = value;
                OnChange?.Invoke();
            }
        }

        public void RequestDocumentTitleEdit(string title)
        {
            DocumentTitle = title;
            DocumentTitleEdited?.Invoke(title);
        }
    }
}
