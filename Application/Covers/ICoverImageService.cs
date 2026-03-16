using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.Shared;

namespace WriterApp.Application.Covers
{
    public interface ICoverImageService
    {
        Task<List<string>> GenerateCoverConceptsAsync(CoverPrompt prompt, CancellationToken ct = default);
    }
}
