using System.Collections.Generic;

namespace WriterApp.Application.Security
{
    public sealed class AuthMeDto
    {
        public bool IsAuthenticated { get; init; }
        public string? Name { get; init; }
        public string? UserId { get; init; }
        public IReadOnlyList<string> Roles { get; init; } = new List<string>();
    }
}
