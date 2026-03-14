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
        string? DeletedAccountMessage = null)
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
    }
}
