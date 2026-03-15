using System;
using WriterApp.Application.Usage;

namespace WriterApp.Tests
{
    internal sealed class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow)
        {
            UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        }

        public DateTime UtcNow { get; }
    }
}
