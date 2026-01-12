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

        public bool CanUndo => _undoStack.Count > 0;

        public bool CanRedo => _redoStack.Count > 0;

        public void ClearHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        public void Execute(IDocumentCommand command)
        {
            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            command.Execute(_state.Document);
            if (command is IAiEditCommand aiCommand)
            {
                AppendAiCommand(_state.Document, aiCommand);
            }

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
            if (command is IAiEditCommand aiCommand)
            {
                RemoveAiCommand(_state.Document, aiCommand);
            }

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
            if (command is IAiEditCommand aiCommand)
            {
                AppendAiCommand(_state.Document, aiCommand);
            }

            _undoStack.Push(command);
            _state.NotifyChanged();
        }

        public void RollbackLastAiEdit(Guid sectionId)
        {
            if (sectionId == Guid.Empty)
            {
                throw new ArgumentException("Section ID is required.", nameof(sectionId));
            }

            if (!TryGetSection(_state.Document, sectionId, out Chapter chapter, out int sectionIndex, out Section section))
            {
                return;
            }

            List<AiEditGroupEntry> groups = section.AI?.AiEditGroups ?? new List<AiEditGroupEntry>();
            if (groups.Count == 0)
            {
                return;
            }

            AiEditGroupEntry group = groups[groups.Count - 1];
            RollbackAiGroup(sectionId, group.GroupId);
        }

        public void RollbackAllAiEdits(Guid sectionId)
        {
            if (sectionId == Guid.Empty)
            {
                throw new ArgumentException("Section ID is required.", nameof(sectionId));
            }

            if (!TryGetSection(_state.Document, sectionId, out Chapter chapter, out int sectionIndex, out Section section))
            {
                return;
            }

            List<AiEditGroupEntry> groups = section.AI?.AiEditGroups ?? new List<AiEditGroupEntry>();
            if (groups.Count == 0)
            {
                return;
            }

            for (int index = groups.Count - 1; index >= 0; index--)
            {
                RollbackAiGroup(sectionId, groups[index].GroupId);
            }
        }

        private void RollbackAiGroup(Guid sectionId, Guid groupId)
        {
            if (_undoStack.Count == 0)
            {
                return;
            }

            Stack<IDocumentCommand> preserved = new();
            while (_undoStack.Count > 0)
            {
                IDocumentCommand command = _undoStack.Pop();
                if (command is IAiEditCommand aiCommand
                    && aiCommand.SectionId == sectionId
                    && aiCommand.AiEditGroupId == groupId)
                {
                    command.Undo(_state.Document);
                    RemoveAiCommand(_state.Document, aiCommand);
                    continue;
                }

                preserved.Push(command);
            }

            while (preserved.Count > 0)
            {
                _undoStack.Push(preserved.Pop());
            }

            _redoStack.Clear();
            _state.NotifyChanged();
        }

        private static void AppendAiCommand(Document document, IAiEditCommand command)
        {
            if (!TryGetSection(document, command.SectionId, out Chapter chapter, out int sectionIndex, out Section section))
            {
                return;
            }

            SectionAIInfo aiInfo = section.AI ?? new SectionAIInfo();
            List<AiEditGroupEntry> groups = aiInfo.AiEditGroups is null
                ? new List<AiEditGroupEntry>()
                : new List<AiEditGroupEntry>(aiInfo.AiEditGroups);

            int groupIndex = groups.FindLastIndex(entry => entry.GroupId == command.AiEditGroupId);
            if (groupIndex < 0)
            {
                groups.Add(new AiEditGroupEntry
                {
                    GroupId = command.AiEditGroupId,
                    AppliedUtc = command.AppliedUtc == default ? DateTime.UtcNow : command.AppliedUtc,
                    Reason = command.AiEditGroupReason,
                    CommandIds = new List<Guid> { command.CommandId }
                });
            }
            else
            {
                AiEditGroupEntry existing = groups[groupIndex];
                List<Guid> commandIds = existing.CommandIds is null
                    ? new List<Guid>()
                    : new List<Guid>(existing.CommandIds);
                if (!commandIds.Contains(command.CommandId))
                {
                    commandIds.Add(command.CommandId);
                }

                groups[groupIndex] = existing with
                {
                    Reason = existing.Reason ?? command.AiEditGroupReason,
                    CommandIds = commandIds
                };
            }

            Section updatedSection = section with
            {
                AI = aiInfo with
                {
                    LastModifiedByAi = true,
                    AiEditGroups = groups
                }
            };
            chapter.Sections[sectionIndex] = updatedSection;
        }

        private static void RemoveAiCommand(Document document, IAiEditCommand command)
        {
            if (!TryGetSection(document, command.SectionId, out Chapter chapter, out int sectionIndex, out Section section))
            {
                return;
            }

            SectionAIInfo aiInfo = section.AI ?? new SectionAIInfo();
            List<AiEditGroupEntry> groups = aiInfo.AiEditGroups is null
                ? new List<AiEditGroupEntry>()
                : new List<AiEditGroupEntry>(aiInfo.AiEditGroups);

            int groupIndex = groups.FindLastIndex(entry => entry.GroupId == command.AiEditGroupId);
            if (groupIndex < 0)
            {
                return;
            }

            AiEditGroupEntry existing = groups[groupIndex];
            List<Guid> commandIds = existing.CommandIds is null
                ? new List<Guid>()
                : new List<Guid>(existing.CommandIds);

            if (commandIds.Remove(command.CommandId))
            {
                if (commandIds.Count == 0)
                {
                    groups.RemoveAt(groupIndex);
                }
                else
                {
                    groups[groupIndex] = existing with { CommandIds = commandIds };
                }
            }

            Section updatedSection = section with
            {
                AI = aiInfo with
                {
                    LastModifiedByAi = groups.Count > 0,
                    AiEditGroups = groups
                }
            };
            chapter.Sections[sectionIndex] = updatedSection;
        }

        private static bool TryGetSection(Document document, Guid sectionId, out Chapter chapter, out int sectionIndex, out Section section)
        {
            for (int chapterIndex = 0; chapterIndex < document.Chapters.Count; chapterIndex++)
            {
                Chapter candidate = document.Chapters[chapterIndex];
                for (int index = 0; index < candidate.Sections.Count; index++)
                {
                    Section entry = candidate.Sections[index];
                    if (entry.SectionId == sectionId)
                    {
                        chapter = candidate;
                        sectionIndex = index;
                        section = entry;
                        return true;
                    }
                }
            }

            chapter = null!;
            sectionIndex = -1;
            section = null!;
            return false;
        }
    }
}
