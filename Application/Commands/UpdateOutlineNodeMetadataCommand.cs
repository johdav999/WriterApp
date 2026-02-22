using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Commands
{
    public sealed class UpdateOutlineNodeMetadataCommand : IStructureUndoCommand
    {
        public UpdateOutlineNodeMetadataCommand(
            string userId,
            Guid documentId,
            Guid nodeId,
            string? beforeMetadataJson,
            string? afterMetadataJson)
        {
            UserId = userId ?? throw new ArgumentNullException(nameof(userId));
            DocumentId = documentId;
            NodeId = nodeId;
            BeforeMetadataJson = beforeMetadataJson;
            AfterMetadataJson = afterMetadataJson;
        }

        public string UserId { get; }

        public Guid DocumentId { get; }

        public Guid NodeId { get; }

        public string? BeforeMetadataJson { get; }

        public string? AfterMetadataJson { get; }

        public Task ExecuteAsync(AppDbContext dbContext, CancellationToken ct)
        {
            return ApplyAsync(dbContext, AfterMetadataJson, ct);
        }

        public Task UndoAsync(AppDbContext dbContext, CancellationToken ct)
        {
            return ApplyAsync(dbContext, BeforeMetadataJson, ct);
        }

        private async Task ApplyAsync(AppDbContext dbContext, string? metadataJson, CancellationToken ct)
        {
            DocumentOutlineNodeRecord? node = await dbContext.DocumentOutlineNodes
                .FirstOrDefaultAsync(item => item.DocumentId == DocumentId && item.Id == NodeId, ct);
            if (node is null)
            {
                return;
            }

            node.MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson.Trim();
        }
    }
}
