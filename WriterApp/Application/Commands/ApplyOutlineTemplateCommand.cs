using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Commands
{
    public sealed class ApplyOutlineTemplateCommand : IStructureUndoCommand
    {
        public ApplyOutlineTemplateCommand(
            string userId,
            Guid documentId,
            IReadOnlyList<DocumentOutlineNodeRecord> nodesToCreate,
            IReadOnlyList<SectionRecord>? sectionsToCreate = null,
            IReadOnlyList<PageRecord>? pagesToCreate = null)
        {
            UserId = userId ?? throw new ArgumentNullException(nameof(userId));
            DocumentId = documentId;
            _nodesToCreate = nodesToCreate ?? throw new ArgumentNullException(nameof(nodesToCreate));
            _sectionsToCreate = sectionsToCreate ?? Array.Empty<SectionRecord>();
            _pagesToCreate = pagesToCreate ?? Array.Empty<PageRecord>();
        }

        private readonly IReadOnlyList<DocumentOutlineNodeRecord> _nodesToCreate;
        private readonly IReadOnlyList<SectionRecord> _sectionsToCreate;
        private readonly IReadOnlyList<PageRecord> _pagesToCreate;
        private readonly List<Guid> _createdNodeIds = new();
        private readonly List<Guid> _createdSectionIds = new();
        private readonly List<Guid> _createdPageIds = new();

        public string UserId { get; }

        public Guid DocumentId { get; }

        public Task ExecuteAsync(AppDbContext dbContext, CancellationToken ct)
        {
            _createdNodeIds.Clear();
            _createdSectionIds.Clear();
            _createdPageIds.Clear();
            foreach (SectionRecord section in _sectionsToCreate)
            {
                _createdSectionIds.Add(section.Id);
                dbContext.Sections.Add(section);
            }

            foreach (PageRecord page in _pagesToCreate)
            {
                _createdPageIds.Add(page.Id);
                dbContext.Pages.Add(page);
            }

            foreach (DocumentOutlineNodeRecord node in _nodesToCreate)
            {
                _createdNodeIds.Add(node.Id);
                dbContext.DocumentOutlineNodes.Add(node);
            }

            return Task.CompletedTask;
        }

        public async Task UndoAsync(AppDbContext dbContext, CancellationToken ct)
        {
            if (_createdNodeIds.Count == 0 && _createdSectionIds.Count == 0 && _createdPageIds.Count == 0)
            {
                return;
            }

            List<DocumentOutlineNodeRecord> created = await dbContext.DocumentOutlineNodes
                .Where(node => node.DocumentId == DocumentId && _createdNodeIds.Contains(node.Id))
                .ToListAsync(ct);
            if (created.Count > 0)
            {
                dbContext.DocumentOutlineNodes.RemoveRange(created);
            }

            if (_createdPageIds.Count > 0)
            {
                List<PageRecord> createdPages = await dbContext.Pages
                    .Where(page => _createdPageIds.Contains(page.Id))
                    .ToListAsync(ct);
                if (createdPages.Count > 0)
                {
                    dbContext.Pages.RemoveRange(createdPages);
                }
            }

            if (_createdSectionIds.Count > 0)
            {
                List<SectionRecord> createdSections = await dbContext.Sections
                    .Where(section => _createdSectionIds.Contains(section.Id))
                    .ToListAsync(ct);
                if (createdSections.Count > 0)
                {
                    dbContext.Sections.RemoveRange(createdSections);
                }
            }
        }
    }
}
