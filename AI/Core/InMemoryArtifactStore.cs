using System;
using System.Collections.Generic;
using WriterApp.AI.Abstractions;

namespace WriterApp.AI.Core
{
    public sealed class InMemoryArtifactStore : IArtifactStore
    {
        private readonly Dictionary<Guid, AiArtifact> _artifacts = new();

        public void Store(AiArtifact artifact)
        {
            if (artifact is null)
            {
                throw new ArgumentNullException(nameof(artifact));
            }

            _artifacts[artifact.ArtifactId] = artifact;
        }

        public AiArtifact? Get(Guid artifactId)
        {
            return _artifacts.TryGetValue(artifactId, out AiArtifact? artifact) ? artifact : null;
        }
    }
}
