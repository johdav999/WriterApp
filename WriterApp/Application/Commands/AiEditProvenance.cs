using System;
using System.Collections.Generic;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    internal static class AiEditProvenance
    {
        internal static void Append(Document document, IAiEditCommand command)
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

        internal static void Remove(Document document, IAiEditCommand command)
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

        internal static bool TryGetSection(Document document, Guid sectionId, out Chapter chapter, out int sectionIndex, out Section section)
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
