using System;
using Microsoft.Extensions.Caching.Memory;

namespace WriterApp.Application.Security
{
    public sealed class AdminEndpointRateLimiter
    {
        private readonly IMemoryCache _cache;

        public AdminEndpointRateLimiter(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public bool TryAcquire(string key, int limitPerMinute)
        {
            if (limitPerMinute <= 0)
            {
                return true;
            }

            string cacheKey = $"admin-rate:{key}";
            Counter counter = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return new Counter();
            })!;

            lock (counter.Sync)
            {
                counter.Count++;
                return counter.Count <= limitPerMinute;
            }
        }

        private sealed class Counter
        {
            public object Sync { get; } = new();
            public int Count { get; set; }
        }
    }
}
