using System.Collections.Generic;
using System.Threading.Tasks;

namespace WriterApp.AI.Abstractions
{
    public sealed record AiStreamingSession(IAsyncEnumerable<AiStreamEvent> Events, Task<AiProposal?> Completion);
}
