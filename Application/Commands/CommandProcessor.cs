using System;
using System.Collections.Generic;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    /// <summary>
    /// Executes document commands and manages undo/redo stacks.
    /// </summary>
    public sealed class CommandProcessor
    {
        private readonly DocumentState _state;
        private readonly Stack<IDocumentCommand> _undoStack = new();
        private readonly Stack<IDocumentCommand> _redoStack = new();

        public CommandProcessor(DocumentState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public void Execute(IDocumentCommand command)
        {
            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            command.Execute(_state.Document);
            _undoStack.Push(command);
            _redoStack.Clear();
            _state.NotifyChanged();
        }

        public void Undo()
        {
            if (_undoStack.Count == 0)
            {
                throw new InvalidOperationException("No command available to undo.");
            }

            IDocumentCommand command = _undoStack.Pop();
            command.Undo(_state.Document);
            _redoStack.Push(command);
            _state.NotifyChanged();
        }

        public void Redo()
        {
            if (_redoStack.Count == 0)
            {
                throw new InvalidOperationException("No command available to redo.");
            }

            IDocumentCommand command = _redoStack.Pop();
            command.Execute(_state.Document);
            _undoStack.Push(command);
            _state.NotifyChanged();
        }
    }
}
