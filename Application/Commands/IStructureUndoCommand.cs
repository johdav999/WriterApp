using System;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.Data;

namespace WriterApp.Application.Commands
{
    public interface IStructureUndoCommand
    {
        string UserId { get; }

        Guid DocumentId { get; }

        Task ExecuteAsync(AppDbContext dbContext, CancellationToken ct);

        Task UndoAsync(AppDbContext dbContext, CancellationToken ct);
    }
}
