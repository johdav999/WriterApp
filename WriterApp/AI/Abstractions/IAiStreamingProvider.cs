using System.Collections.Generic;
using System.Threading;

namespace WriterApp.AI.Abstractions
{
    public interface IAiStreamingProvider : IAiProvider
    {
        AiStreamingCapabilities StreamingCapabilities { get; }
        IAsyncEnumerable<AiStreamEvent> StreamAsync(AiRequest request, CancellationToken ct);
    }
}
