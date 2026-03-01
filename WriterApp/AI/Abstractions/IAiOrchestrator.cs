using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WriterApp.AI.Abstractions
{
    public interface IAiOrchestrator
    {
        IReadOnlyList<IAiAction> Actions { get; }
        IAiAction? GetAction(string actionId);
        bool CanRunAction(string actionId);
        AiStreamingCapabilities GetStreamingCapabilities(string actionId);
        Task<AiExecutionResult> ExecuteActionAsync(string actionId, AiActionInput input, CancellationToken ct);
        AiStreamingSession StreamActionAsync(string actionId, AiActionInput input, CancellationToken ct);
    }
}
