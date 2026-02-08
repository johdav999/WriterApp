using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using WriterApp.Data;

namespace WriterApp.Application.Commands
{
    public sealed class StructureCommandProcessor : IStructureCommandProcessor
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ConcurrentDictionary<string, Stack<IStructureUndoCommand>> _undo = new();
        private readonly ConcurrentDictionary<string, Stack<IStructureUndoCommand>> _redo = new();

        public StructureCommandProcessor(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        }

        public async Task ExecuteAsync(IStructureUndoCommand command, CancellationToken ct)
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await command.ExecuteAsync(dbContext, ct);
            await dbContext.SaveChangesAsync(ct);

            string key = BuildKey(command.UserId, command.DocumentId);
            _undo.GetOrAdd(key, _ => new Stack<IStructureUndoCommand>()).Push(command);
            _redo.TryRemove(key, out _);
        }

        public async Task<bool> UndoAsync(string userId, Guid documentId, CancellationToken ct)
        {
            string key = BuildKey(userId, documentId);
            if (!_undo.TryGetValue(key, out Stack<IStructureUndoCommand>? undoStack) || undoStack.Count == 0)
            {
                return false;
            }

            IStructureUndoCommand command = undoStack.Pop();
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await command.UndoAsync(dbContext, ct);
            await dbContext.SaveChangesAsync(ct);

            _redo.GetOrAdd(key, _ => new Stack<IStructureUndoCommand>()).Push(command);
            return true;
        }

        public async Task<bool> RedoAsync(string userId, Guid documentId, CancellationToken ct)
        {
            string key = BuildKey(userId, documentId);
            if (!_redo.TryGetValue(key, out Stack<IStructureUndoCommand>? redoStack) || redoStack.Count == 0)
            {
                return false;
            }

            IStructureUndoCommand command = redoStack.Pop();
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await command.ExecuteAsync(dbContext, ct);
            await dbContext.SaveChangesAsync(ct);

            _undo.GetOrAdd(key, _ => new Stack<IStructureUndoCommand>()).Push(command);
            return true;
        }

        private static string BuildKey(string userId, Guid documentId)
        {
            return $"{userId}:{documentId:N}";
        }
    }
}
