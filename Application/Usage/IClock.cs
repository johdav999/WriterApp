using System;

namespace WriterApp.Application.Usage
{
    public interface IClock
    {
        DateTime UtcNow { get; }
    }
}
