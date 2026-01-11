using System;
using System.Collections.Generic;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    /// <summary>
    /// Executes a set of commands as a single undoable unit.
    /// </summary>
    public sealed class CompositeCommand : IDocumentCommand
    {
        private readonly IReadOnlyList<IDocumentCommand> _commands;
        private bool _hasExecuted;

        public CompositeCommand(IReadOnlyList<IDocumentCommand> commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        public string Name => "CompositeCommand";

        public DateTime ExecutedUtc { get; private set; }

        public void Execute(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            for (int index = 0; index < _commands.Count; index++)
            {
                IDocumentCommand command = _commands[index];
                command.Execute(document);
            }

            if (ExecutedUtc == default)
            {
                ExecutedUtc = DateTime.UtcNow;
            }

            _hasExecuted = true;
        }

        public void Undo(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (!_hasExecuted)
            {
                throw new InvalidOperationException("Command has not been executed.");
            }

            for (int index = _commands.Count - 1; index >= 0; index--)
            {
                IDocumentCommand command = _commands[index];
                command.Undo(document);
            }
        }
    }
}
