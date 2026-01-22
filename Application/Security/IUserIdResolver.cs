using System.Security.Claims;

namespace WriterApp.Application.Security
{
    public interface IUserIdResolver
    {
        string ResolveUserId(ClaimsPrincipal user);
    }
}
