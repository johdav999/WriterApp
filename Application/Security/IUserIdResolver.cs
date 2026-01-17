using System.Security.Claims;

namespace WriterApp.Application.Security
{
    public interface IUserIdResolver
    {
        string ResolveForEntitlements(ClaimsPrincipal? user);
    }
}
