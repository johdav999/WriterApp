using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.Shared;

namespace WriterApp.Application.Covers
{
    public interface ICoverImageService
    {
        Task<List<string>> GenerateCoverConceptsAsync(CoverPrompt prompt, CancellationToken ct = default);
        CoverEditCapabilities EditCapabilities => new(1, [], "This provider configuration does not support cover image edits.");
        Task<string> EditAsync(byte[] image, string operation, CoverPrompt prompt, CancellationToken ct = default)
            => throw new CoverImageGenerationException("cover.edit_unsupported", "This provider configuration does not support cover image edits.");
    }
}
