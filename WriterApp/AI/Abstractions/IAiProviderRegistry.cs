using System.Collections.Generic;

namespace WriterApp.AI.Abstractions
{
    public interface IAiProviderRegistry
    {
        IReadOnlyList<IAiProvider> Providers { get; }
        IAiProvider? GetById(string providerId);
        IReadOnlyList<IAiProvider> GetAll();
        IAiProvider? GetFirstCapable(AiModality modality, bool streamingRequired);
    }
}
