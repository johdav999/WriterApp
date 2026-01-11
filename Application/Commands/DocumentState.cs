using System;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.State
{
    /// <summary>
    /// Holds the current document and notifies listeners on mutations.
    /// </summary>
    public sealed class DocumentState
    {
        public Document Document { get; }

        public event Action? OnChanged;

        public DocumentState(Document document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public void NotifyChanged()
        {
            OnChanged?.Invoke();
        }
    }
}
