using System;
using System.Collections.Generic;
using WriterApp.AI.Abstractions;

namespace WriterApp.AI.Core
{
    public sealed class InMemoryAiAttachmentStore : IAiAttachmentStore
    {
        private readonly Dictionary<Guid, Guid> _coverImages = new();

        public Guid? GetCoverImageId(Guid sectionId)
        {
            return _coverImages.TryGetValue(sectionId, out Guid artifactId) ? artifactId : null;
        }

        public void SetCoverImageId(Guid sectionId, Guid? artifactId)
        {
            if (artifactId is null)
            {
                _coverImages.Remove(sectionId);
                return;
            }

            _coverImages[sectionId] = artifactId.Value;
        }
    }
}
