using System;
using System.Collections.Generic;

namespace WriterApp.Client.Services
{
    public sealed record AuthState(
        bool IsAuthenticated,
        string? Provider,
        string? UserId,
        IReadOnlyDictionary<string, string> Claims,
        bool IsDeletedAccount = false,
        string? DeletedAccountMessage = null,
        bool IsDuplicateAccount = false,
        string? DuplicateAccountMessage = null)
    {
        public static AuthState Anonymous { get; } = new(
            IsAuthenticated: false,
            Provider: null,
            UserId: null,
            Claims: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        public static AuthState DeletedAccount(string? message, string? provider = null) => new(
            IsAuthenticated: false,
            Provider: provider,
            UserId: null,
            Claims: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            IsDeletedAccount: true,
            DeletedAccountMessage: message);

        public static AuthState DuplicateAccount(string? message, string? provider = null) => new(
            IsAuthenticated: false,
            Provider: provider,
            UserId: null,
            Claims: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            IsDuplicateAccount: true,
            DuplicateAccountMessage: message);
    }
}
