using System;
using WriterApp.Application.Security;

namespace WriterApp.Client.State
{
    public sealed class DuplicateAccountStateService
    {
        public const string DefaultMessage = "An account may already exist for this email under a different sign-in method.";

        public event Action? Changed;

        public bool IsDuplicateAccount { get; private set; }
        public string Message { get; private set; } = DefaultMessage;
        public string? CurrentLoginProvider { get; private set; }
        public bool EmailPresent { get; private set; }
        public string? MaskedEmail { get; private set; }
        public string? MatchedUserIdMasked { get; private set; }

        public void MarkDuplicate(AuthDuplicateAccountDto duplicate)
        {
            if (duplicate is null)
            {
                return;
            }

            string message = string.IsNullOrWhiteSpace(duplicate.Message)
                ? DefaultMessage
                : duplicate.Message.Trim();
            bool changed =
                !IsDuplicateAccount
                || !string.Equals(Message, message, StringComparison.Ordinal)
                || !string.Equals(CurrentLoginProvider, duplicate.CurrentLoginProvider, StringComparison.Ordinal)
                || EmailPresent != duplicate.EmailPresent
                || !string.Equals(MaskedEmail, duplicate.MaskedEmail, StringComparison.Ordinal)
                || !string.Equals(MatchedUserIdMasked, duplicate.MatchedUserIdMasked, StringComparison.Ordinal);

            IsDuplicateAccount = true;
            Message = message;
            CurrentLoginProvider = duplicate.CurrentLoginProvider;
            EmailPresent = duplicate.EmailPresent;
            MaskedEmail = duplicate.MaskedEmail;
            MatchedUserIdMasked = duplicate.MatchedUserIdMasked;

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        public void Clear()
        {
            if (!IsDuplicateAccount
                && string.Equals(Message, DefaultMessage, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(CurrentLoginProvider)
                && !EmailPresent
                && string.IsNullOrWhiteSpace(MaskedEmail)
                && string.IsNullOrWhiteSpace(MatchedUserIdMasked))
            {
                return;
            }

            IsDuplicateAccount = false;
            Message = DefaultMessage;
            CurrentLoginProvider = null;
            EmailPresent = false;
            MaskedEmail = null;
            MatchedUserIdMasked = null;
            Changed?.Invoke();
        }
    }
}
