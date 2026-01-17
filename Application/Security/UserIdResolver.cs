using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WriterApp.Application.Security
{
    public sealed class UserIdResolver : IUserIdResolver
    {
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly ILogger<UserIdResolver> _logger;
        private bool _hasLoggedResolvedUserId;

        public UserIdResolver(IWebHostEnvironment hostEnvironment, ILogger<UserIdResolver> logger)
        {
            _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string ResolveForEntitlements(ClaimsPrincipal? user)
        {
            string userId = user.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("UserId was empty; falling back to DEV user in development mode");
                if (_hostEnvironment.IsDevelopment())
                {
                    userId = "DEV";
                }
            }

            if (!_hasLoggedResolvedUserId)
            {
                _hasLoggedResolvedUserId = true;
                _logger.LogInformation("Resolved UserId for entitlements: {UserId}", userId);
            }

            return userId;
        }
    }
}
