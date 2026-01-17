using System.Security.Claims;

namespace WriterApp.Application.Security
{
    public static class ClaimsPrincipalExtensions
    {
        public static string GetUserId(this ClaimsPrincipal? user)
        {
            if (user is null)
            {
                return string.Empty;
            }

            return user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        }
    }
}
