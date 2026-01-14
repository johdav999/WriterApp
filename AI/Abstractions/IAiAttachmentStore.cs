using System;

namespace WriterApp.AI.Abstractions
{
    public interface IAiAttachmentStore
    {
        Guid? GetCoverImageId(Guid sectionId);
        void SetCoverImageId(Guid sectionId, Guid? artifactId);
    }
}
