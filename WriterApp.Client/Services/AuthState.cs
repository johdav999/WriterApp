using System;
using System.Collections.Generic;

namespace WriterApp.Client.Services
{
    public sealed record AuthState(
        bool IsAuthenticated,
        string? Provider,
        string? UserId,
        IReadOnlyDictionary<string, string> Claims)
    {
        public static AuthState Anonymous { get; } = new(
            IsAuthenticated: false,
            Provider: null,
            UserId: null,
            Claims: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
