using System;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    /// <summary>
    /// Represents a reversible document mutation for undo/redo workflows.
    /// </summary>
    public interface IDocumentCommand
    {
        string Name { get; }
        DateTime ExecutedUtc { get; }
        void Execute(Document document);
        void Undo(Document document);
    }
}
