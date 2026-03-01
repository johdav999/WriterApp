using System.Threading;
using System.Threading.Tasks;

namespace WriterApp.AI.Abstractions
{
    public interface IAiProvider
    {
        string ProviderId { get; }
        AiProviderCapabilities Capabilities { get; }
        Task<AiResult> ExecuteAsync(AiRequest request, CancellationToken ct);
    }
}
