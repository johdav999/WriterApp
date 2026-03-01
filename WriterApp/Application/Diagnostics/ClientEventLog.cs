using System;
using System.Collections.Generic;

namespace WriterApp.Application.Diagnostics
{
    public enum ClientEventLevel
    {
        Info,
        Warn,
        Error
    }

    public sealed class ClientEvent
    {
        public ClientEvent(
            DateTimeOffset timestampUtc,
            ClientEventLevel level,
            string category,
            string message,
            string? exception)
        {
            TimestampUtc = timestampUtc;
            Level = level;
            Category = category ?? string.Empty;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public DateTimeOffset TimestampUtc { get; }
        public ClientEventLevel Level { get; }
        public string Category { get; }
        public string Message { get; }
        public string? Exception { get; }
    }

    public sealed class ClientEventLog
    {
        private readonly int _capacity;
        private readonly List<ClientEvent> _events = new();
        private readonly object _lock = new();

        public ClientEventLog(int capacity = 200)
        {
            _capacity = Math.Max(10, capacity);
        }

        public event Action? OnChanged;

        public IReadOnlyList<ClientEvent> Events
        {
            get
            {
                lock (_lock)
                {
                    return _events.ToArray();
                }
            }
        }

        public void Info(string category, string message) =>
            Add(ClientEventLevel.Info, category, message, null);

        public void Warn(string category, string message, Exception? ex = null) =>
            Add(ClientEventLevel.Warn, category, message, ex);

        public void Error(string category, string message, Exception? ex = null) =>
            Add(ClientEventLevel.Error, category, message, ex);

        private void Add(ClientEventLevel level, string category, string message, Exception? ex)
        {
            ClientEvent entry = new(
                DateTimeOffset.UtcNow,
                level,
                category,
                message,
                ex?.ToString());

            lock (_lock)
            {
                _events.Add(entry);
                int overflow = _events.Count - _capacity;
                if (overflow > 0)
                {
                    _events.RemoveRange(0, overflow);
                }
            }

            OnChanged?.Invoke();
        }
    }
}
