using System.Threading;
using System.Threading.Tasks;

namespace WriterApp.AI.Abstractions
{
    public interface IAiImageProvider
    {
        Task<AiImageResult> GenerateImageAsync(AiRequest request, CancellationToken ct);
    }
}
