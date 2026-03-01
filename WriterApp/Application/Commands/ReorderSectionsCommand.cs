using System;
using System.Collections.Generic;
using System.Linq;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Commands
{
    /// <summary>
    /// Reorders sections within a chapter by updating their Order values.
    /// </summary>
    public sealed class ReorderSectionsCommand : IDocumentCommand
    {
        private readonly Guid _chapterId;
        private readonly IReadOnlyList<Guid> _orderedSectionIds;
        private Dictionary<Guid, int>? _previousOrders;
        private bool _hasExecuted;

        public ReorderSectionsCommand(Guid chapterId, IReadOnlyList<Guid> orderedSectionIds)
        {
            if (chapterId == Guid.Empty)
            {
                throw new ArgumentException("Chapter ID is required.", nameof(chapterId));
            }

            _chapterId = chapterId;
            _orderedSectionIds = orderedSectionIds ?? throw new ArgumentNullException(nameof(orderedSectionIds));
            if (_orderedSectionIds.Count == 0)
            {
                throw new ArgumentException("Section order list must not be empty.", nameof(orderedSectionIds));
            }
        }

        public string Name => "ReorderSections";

        public DateTime ExecutedUtc { get; private set; }

        public void Execute(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            Chapter chapter = FindChapter(document, _chapterId);
            ValidateOrdering(chapter, _orderedSectionIds);

            _previousOrders = chapter.Sections.ToDictionary(section => section.SectionId, section => section.Order);

            Dictionary<Guid, int> targetOrders = new();
            for (int i = 0; i < _orderedSectionIds.Count; i++)
            {
                targetOrders[_orderedSectionIds[i]] = i;
            }

            for (int i = 0; i < chapter.Sections.Count; i++)
            {
                Section section = chapter.Sections[i];
                if (targetOrders.TryGetValue(section.SectionId, out int order))
                {
                    chapter.Sections[i] = section with { Order = order };
                }
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

            if (!_hasExecuted || _previousOrders is null)
            {
                throw new InvalidOperationException("Command has not been executed.");
            }

            Chapter chapter = FindChapter(document, _chapterId);
            for (int i = 0; i < chapter.Sections.Count; i++)
            {
                Section section = chapter.Sections[i];
                if (_previousOrders.TryGetValue(section.SectionId, out int order))
                {
                    chapter.Sections[i] = section with { Order = order };
                }
            }
        }

        private static Chapter FindChapter(Document document, Guid chapterId)
        {
            for (int index = 0; index < document.Chapters.Count; index++)
            {
                Chapter chapter = document.Chapters[index];
                if (chapter.ChapterId == chapterId)
                {
                    return chapter;
                }
            }

            throw new InvalidOperationException($"Chapter {chapterId} was not found.");
        }

        private static void ValidateOrdering(Chapter chapter, IReadOnlyList<Guid> orderedSectionIds)
        {
            if (orderedSectionIds.Count != chapter.Sections.Count)
            {
                throw new InvalidOperationException("Section order list must include every section in the chapter.");
            }

            HashSet<Guid> uniqueIds = new();
            foreach (Guid sectionId in orderedSectionIds)
            {
                if (!uniqueIds.Add(sectionId))
                {
                    throw new InvalidOperationException("Section order list contains duplicate section IDs.");
                }
            }

            foreach (Section section in chapter.Sections)
            {
                if (!uniqueIds.Contains(section.SectionId))
                {
                    throw new InvalidOperationException("Section order list is missing a section ID.");
                }
            }
        }
    }
}
