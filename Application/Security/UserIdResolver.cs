using System;
using System.Security;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace WriterApp.Application.Security
{
    public sealed class UserIdResolver : IUserIdResolver
    {
        private readonly ILogger<UserIdResolver> _logger;
        private bool _hasLoggedResolvedUserId;

        public UserIdResolver(
            ILogger<UserIdResolver> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string ResolveUserId(ClaimsPrincipal user)
        {
            if (user is null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            string userId =
                user.FindFirstValue("oid") ??
                user.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier") ??
                string.Empty;
            _logger.LogInformation(
                "Server Auth: IsAuthenticated={Auth}, Name={Name}",
                user.Identity?.IsAuthenticated,
                user.Identity?.Name);

            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogError("Authenticated user missing oid claim.");
                throw new SecurityException("Authenticated user missing oid claim");
            }

            if (!_hasLoggedResolvedUserId)
            {
                _hasLoggedResolvedUserId = true;
                _logger.LogInformation("Resolved UserId for request: {UserId}", userId);
            }

            return userId;
        }
    }
}
