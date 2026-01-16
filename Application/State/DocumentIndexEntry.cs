using System;

namespace WriterApp.Application.State
{
    public sealed record DocumentIndexEntry(Guid DocumentId, string Title, DateTime LastModifiedUtc, int WordCount);
}
