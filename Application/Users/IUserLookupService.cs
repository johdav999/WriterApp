using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WriterApp.Application.Users
{
    public interface IUserLookupService
    {
        Task<UserLookupResult?> FindByEmailAsync(string email, CancellationToken ct = default);
        Task<UserLookupUser?> FindByUserIdAsync(string userId, CancellationToken ct = default);
    }

    public sealed record UserLookupResult(string QueryEmail, IReadOnlyList<UserLookupUser> Matches);

    public sealed record UserLookupUser(string UserId, string? DisplayName, string? Email);
}
