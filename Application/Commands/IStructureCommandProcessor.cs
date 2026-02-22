using System;
using System.Threading;
using System.Threading.Tasks;

namespace WriterApp.Application.Commands
{
    public interface IStructureCommandProcessor
    {
        Task ExecuteAsync(IStructureUndoCommand command, CancellationToken ct);

        Task<bool> UndoAsync(string userId, Guid documentId, CancellationToken ct);

        Task<bool> RedoAsync(string userId, Guid documentId, CancellationToken ct);
    }
}
