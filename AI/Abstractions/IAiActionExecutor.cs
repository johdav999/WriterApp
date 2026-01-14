using System.Threading;
using System.Threading.Tasks;

namespace WriterApp.AI.Abstractions
{
    public interface IAiActionExecutor
    {
        Task<AiProposal> ExecuteAsync(IAiAction action, AiActionInput input, CancellationToken ct);
    }
}
