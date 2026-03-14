using System;

namespace WriterApp.Client.State
{
    public sealed class DeletedAccountStateService
    {
        public const string DefaultMessage = "This Prosa account has been deleted. Sign out before registering again.";

        public event Action? Changed;

        public bool IsDeletedAccount { get; private set; }

        public string Message { get; private set; } = DefaultMessage;

        public void MarkDeleted(string? message)
        {
            string nextMessage = string.IsNullOrWhiteSpace(message)
                ? DefaultMessage
                : message.Trim();

            bool changed = !IsDeletedAccount || !string.Equals(Message, nextMessage, StringComparison.Ordinal);
            IsDeletedAccount = true;
            Message = nextMessage;

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        public void Clear()
        {
            if (!IsDeletedAccount && string.Equals(Message, DefaultMessage, StringComparison.Ordinal))
            {
                return;
            }

            IsDeletedAccount = false;
            Message = DefaultMessage;
            Changed?.Invoke();
        }
    }
}
