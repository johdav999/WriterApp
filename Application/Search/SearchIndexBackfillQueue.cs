using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace WriterApp.Application.Search
{
    public interface ISearchIndexBackfillQueue
    {
        bool Enqueue(string userId);
        ValueTask<string> DequeueAsync(CancellationToken ct);
        void MarkCompleted(string userId);
    }

    public sealed class SearchIndexBackfillQueue : ISearchIndexBackfillQueue
    {
        private readonly Channel<string> _channel;
        private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, byte> _inFlight = new(StringComparer.Ordinal);

        public SearchIndexBackfillQueue()
        {
            _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        }

        public bool Enqueue(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            if (_pending.ContainsKey(userId) || _inFlight.ContainsKey(userId))
            {
                return false;
            }

            if (!_pending.TryAdd(userId, 0))
            {
                return false;
            }

            if (!_channel.Writer.TryWrite(userId))
            {
                _pending.TryRemove(userId, out _);
                return false;
            }

            return true;
        }

        public async ValueTask<string> DequeueAsync(CancellationToken ct)
        {
            string userId = await _channel.Reader.ReadAsync(ct);
            _pending.TryRemove(userId, out _);
            _inFlight.TryAdd(userId, 0);
            return userId;
        }

        public void MarkCompleted(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            _inFlight.TryRemove(userId, out _);
        }
    }
}
