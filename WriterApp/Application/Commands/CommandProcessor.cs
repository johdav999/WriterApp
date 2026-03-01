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
                AiEditProvenance.Append(_state.Document, aiCommand);
            }

            _undoStack.Push(command);
            _redoStack.Clear();
            _state.NotifyChanged();
        }

        public void AppendAiHistoryEntry(Guid sectionId, AIHistoryEntry entry)
        {
            if (entry is null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (!AiEditProvenance.TryGetSection(_state.Document, sectionId, out Chapter chapter, out int sectionIndex, out Section section))
            {
                return;
            }

            SectionAIInfo aiInfo = section.AI;
            if (aiInfo is null)
            {
                return;
            }

            List<AIHistoryEntry> history = aiInfo.AIHistory is null
                ? new List<AIHistoryEntry>()
                : new List<AIHistoryEntry>(aiInfo.AIHistory);

            if (history.Exists(existing => existing.EditGroupId == entry.EditGroupId))
            {
                return;
            }

            history.Add(entry);
            Section updatedSection = section with
            {
                AI = aiInfo with { AIHistory = history }
            };
            chapter.Sections[sectionIndex] = updatedSection;
            _state.NotifyChanged();
        }

        public void RemoveAiHistoryEntry(Guid sectionId, Guid editGroupId)
        {
            if (editGroupId == Guid.Empty)
            {
                return;
            }

            if (!AiEditProvenance.TryGetSection(_state.Document, sectionId, out Chapter chapter, out int sectionIndex, out Section section))
            {
                return;
            }

            SectionAIInfo aiInfo = section.AI;
            if (aiInfo is null || aiInfo.AIHistory is null || aiInfo.AIHistory.Count == 0)
            {
                return;
            }

            List<AIHistoryEntry> history = new List<AIHistoryEntry>(aiInfo.AIHistory);
            int removedCount = history.RemoveAll(entry => entry.EditGroupId == editGroupId);
            if (removedCount == 0)
            {
                return;
            }

            Section updatedSection = section with
            {
                AI = aiInfo with { AIHistory = history }
            };
            chapter.Sections[sectionIndex] = updatedSection;
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
                AiEditProvenance.Remove(_state.Document, aiCommand);
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
                AiEditProvenance.Append(_state.Document, aiCommand);
            }

            _undoStack.Push(command);
            _state.NotifyChanged();
        }

        public AiEditSelectionInfo GetAiEditSelectionInfo(Guid sectionId, TextRange range, int sectionPlainTextLength)
        {
            if (sectionId == Guid.Empty)
            {
                return new AiEditSelectionInfo(false, null, false);
            }

            if (!AiEditProvenance.TryGetSection(_state.Document, sectionId, out Chapter chapter, out int sectionIndex, out Section section))
            {
                return new AiEditSelectionInfo(false, null, false);
            }

            List<AiEditGroupEntry> groups = section.AI?.AiEditGroups ?? new List<AiEditGroupEntry>();
            bool hasMultipleGroups = groups.Count > 1;
            if (groups.Count == 0)
            {
                return new AiEditSelectionInfo(false, null, false);
            }

            HashSet<Guid> groupIds = new();
            for (int index = 0; index < groups.Count; index++)
            {
                groupIds.Add(groups[index].GroupId);
            }

            foreach (IDocumentCommand command in _undoStack)
            {
                if (command is not IAiEditCommand aiCommand)
                {
                    continue;
                }

                if (aiCommand.SectionId != sectionId || !groupIds.Contains(aiCommand.AiEditGroupId))
                {
                    continue;
                }

                TextRange targetRange = GetAiCommandRange(aiCommand, sectionPlainTextLength);
                if (RangesIntersect(range, targetRange))
                {
                    return new AiEditSelectionInfo(true, aiCommand.AiEditGroupId, hasMultipleGroups);
                }
            }

            return new AiEditSelectionInfo(false, null, hasMultipleGroups);
        }

        public IReadOnlyList<AiEditRangeInfo> GetAiEditRanges(Guid sectionId, int sectionPlainTextLength)
        {
            if (sectionId == Guid.Empty)
            {
                return Array.Empty<AiEditRangeInfo>();
            }

            if (!AiEditProvenance.TryGetSection(_state.Document, sectionId, out Chapter chapter, out int sectionIndex, out Section section))
            {
                return Array.Empty<AiEditRangeInfo>();
            }

            List<AiEditGroupEntry> groups = section.AI?.AiEditGroups ?? new List<AiEditGroupEntry>();
            if (groups.Count == 0)
            {
                return Array.Empty<AiEditRangeInfo>();
            }

            HashSet<Guid> groupIds = new();
            for (int index = 0; index < groups.Count; index++)
            {
                groupIds.Add(groups[index].GroupId);
            }

            Dictionary<Guid, TextRange> mergedRanges = new();
            foreach (IDocumentCommand command in _undoStack)
            {
                if (command is not IAiEditCommand aiCommand)
                {
                    continue;
                }

                if (aiCommand.SectionId != sectionId || !groupIds.Contains(aiCommand.AiEditGroupId))
                {
                    continue;
                }

                TextRange range = GetAiCommandRange(aiCommand, sectionPlainTextLength);
                if (mergedRanges.TryGetValue(aiCommand.AiEditGroupId, out TextRange existing))
                {
                    int start = Math.Min(existing.Start, range.Start);
                    int end = Math.Max(existing.Start + existing.Length, range.Start + range.Length);
                    mergedRanges[aiCommand.AiEditGroupId] = new TextRange(start, Math.Max(0, end - start));
                }
                else
                {
                    mergedRanges[aiCommand.AiEditGroupId] = range;
                }
            }

            if (mergedRanges.Count == 0)
            {
                return Array.Empty<AiEditRangeInfo>();
            }

            List<AiEditRangeInfo> results = new(mergedRanges.Count);
            foreach (KeyValuePair<Guid, TextRange> entry in mergedRanges)
            {
                results.Add(new AiEditRangeInfo(entry.Value, entry.Key));
            }

            results.Sort((left, right) => left.Range.Start.CompareTo(right.Range.Start));
            return results;
        }

        public bool RollbackAiEditGroup(Guid sectionId, Guid groupId)
        {
            if (sectionId == Guid.Empty)
            {
                throw new ArgumentException("Section ID is required.", nameof(sectionId));
            }

            if (groupId == Guid.Empty)
            {
                throw new ArgumentException("Group ID is required.", nameof(groupId));
            }

            if (!AiEditProvenance.TryGetSection(_state.Document, sectionId, out Chapter chapter, out int sectionIndex, out Section section))
            {
                return false;
            }

            List<AiEditGroupEntry> groups = section.AI?.AiEditGroups ?? new List<AiEditGroupEntry>();
            if (groups.Count == 0 || !groups.Exists(entry => entry.GroupId == groupId))
            {
                return false;
            }

            List<IAiEditCommand> commandsInOrder = CollectAiGroupCommands(sectionId, groupId);
            if (commandsInOrder.Count == 0)
            {
                return false;
            }

            Execute(new RollbackAiEditGroupCommand(sectionId, groupId, commandsInOrder));
            return true;
        }

        public void RollbackLastAiEdit(Guid sectionId)
        {
            if (sectionId == Guid.Empty)
            {
                throw new ArgumentException("Section ID is required.", nameof(sectionId));
            }

            if (!AiEditProvenance.TryGetSection(_state.Document, sectionId, out Chapter chapter, out int sectionIndex, out Section section))
            {
                return;
            }

            List<AiEditGroupEntry> groups = section.AI?.AiEditGroups ?? new List<AiEditGroupEntry>();
            if (groups.Count == 0)
            {
                return;
            }

            AiEditGroupEntry group = groups[^1];
            RollbackAiEditGroup(sectionId, group.GroupId);
        }

        public void RollbackAllAiEdits(Guid sectionId)
        {
            if (sectionId == Guid.Empty)
            {
                throw new ArgumentException("Section ID is required.", nameof(sectionId));
            }

            if (!AiEditProvenance.TryGetSection(_state.Document, sectionId, out Chapter chapter, out int sectionIndex, out Section section))
            {
                return;
            }

            List<AiEditGroupEntry> groups = section.AI?.AiEditGroups ?? new List<AiEditGroupEntry>();
            if (groups.Count == 0)
            {
                return;
            }

            List<Guid> groupIds = new();
            for (int index = 0; index < groups.Count; index++)
            {
                groupIds.Add(groups[index].GroupId);
            }

            for (int index = groupIds.Count - 1; index >= 0; index--)
            {
                RollbackAiEditGroup(sectionId, groupIds[index]);
            }
        }

        private List<IAiEditCommand> CollectAiGroupCommands(Guid sectionId, Guid groupId)
        {
            List<IAiEditCommand> commands = new();
            foreach (IDocumentCommand command in _undoStack)
            {
                if (command is not IAiEditCommand aiCommand)
                {
                    continue;
                }

                if (aiCommand.SectionId == sectionId && aiCommand.AiEditGroupId == groupId)
                {
                    commands.Add(aiCommand);
                }
            }

            commands.Reverse();
            return commands;
        }

        private static TextRange GetAiCommandRange(IAiEditCommand command, int sectionPlainTextLength)
        {
            if (command is IAiRangeEditCommand rangedCommand)
            {
                return rangedCommand.Range;
            }

            return new TextRange(0, Math.Max(0, sectionPlainTextLength));
        }

        private static bool RangesIntersect(TextRange selection, TextRange target)
        {
            if (selection.Length == 0)
            {
                int point = selection.Start;
                return point >= target.Start && point <= target.Start + target.Length;
            }

            int selectionEnd = selection.Start + selection.Length;
            int targetEnd = target.Start + target.Length;
            return selection.Start < targetEnd && target.Start < selectionEnd;
        }
    }
}
