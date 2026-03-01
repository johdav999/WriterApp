using System;
using System.Collections.Generic;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    public sealed class RollbackAiEditGroupCommand : IDocumentCommand
    {
        private readonly Guid _sectionId;
        private readonly Guid _groupId;
        private readonly IReadOnlyList<IAiEditCommand> _commandsInOrder;
        private bool _hasExecuted;

        public RollbackAiEditGroupCommand(Guid sectionId, Guid groupId, IReadOnlyList<IAiEditCommand> commandsInOrder)
        {
            if (sectionId == Guid.Empty)
            {
                throw new ArgumentException("Section ID is required.", nameof(sectionId));
            }

            if (groupId == Guid.Empty)
            {
                throw new ArgumentException("Group ID is required.", nameof(groupId));
            }

            _commandsInOrder = commandsInOrder ?? throw new ArgumentNullException(nameof(commandsInOrder));
            if (_commandsInOrder.Count == 0)
            {
                throw new ArgumentException("At least one command is required for rollback.", nameof(commandsInOrder));
            }

            _sectionId = sectionId;
            _groupId = groupId;
        }

        public string Name => "RollbackAiEditGroup";

        public DateTime ExecutedUtc { get; private set; }

        public void Execute(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            for (int index = _commandsInOrder.Count - 1; index >= 0; index--)
            {
                IAiEditCommand command = _commandsInOrder[index];
                if (command.SectionId != _sectionId || command.AiEditGroupId != _groupId)
                {
                    continue;
                }

                command.Undo(document);
                AiEditProvenance.Remove(document, command);
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

            for (int index = 0; index < _commandsInOrder.Count; index++)
            {
                IAiEditCommand command = _commandsInOrder[index];
                if (command.SectionId != _sectionId || command.AiEditGroupId != _groupId)
                {
                    continue;
                }

                command.Execute(document);
                AiEditProvenance.Append(document, command);
            }
        }
    }
}
