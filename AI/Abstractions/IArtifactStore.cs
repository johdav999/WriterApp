using System;

namespace WriterApp.AI.Abstractions
{
    public interface IArtifactStore
    {
        void Store(AiArtifact artifact);
        AiArtifact? Get(Guid artifactId);
    }
}
