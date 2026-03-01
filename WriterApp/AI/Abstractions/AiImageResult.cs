using System.Collections.Generic;

namespace WriterApp.AI.Abstractions
{
    public sealed record AiImageResult(
        byte[] ImageBytes,
        string ContentType,
        Dictionary<string, object> ProviderMetadata);
}
