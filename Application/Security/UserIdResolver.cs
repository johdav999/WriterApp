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

            ExternalIdentityClaims.ExternalIdentityResolution? resolution =
                ExternalIdentityClaims.ResolveIdentity(user.Claims);
            string userId = resolution?.UserId ?? string.Empty;
            _logger.LogInformation(
                "Server Auth: IsAuthenticated={Auth}, Name={Name}",
                user.Identity?.IsAuthenticated,
                user.Identity?.Name);

            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogError(
                    "Authenticated user missing canonical identity claims. Strategy={Strategy} HasOid={HasOid} HasIssuer={HasIssuer} HasSubject={HasSubject} HasSid={HasSid}",
                    ExternalIdentityClaims.DescribeResolutionStrategy(user.Claims),
                    !string.IsNullOrWhiteSpace(ExternalIdentityClaims.ResolveOid(user.Claims)),
                    !string.IsNullOrWhiteSpace(ExternalIdentityClaims.ResolveIssuer(user.Claims)),
                    !string.IsNullOrWhiteSpace(ExternalIdentityClaims.ResolveSubject(user.Claims)),
                    !string.IsNullOrWhiteSpace(ExternalIdentityClaims.ResolveSid(user.Claims)));
                throw new SecurityException("Authenticated user missing canonical identity claims (legacy oid or issuer+sub or sid)");
            }

            if (!_hasLoggedResolvedUserId)
            {
                _hasLoggedResolvedUserId = true;
                _logger.LogInformation(
                    "Resolved canonical user identity. Strategy={Strategy} UserId={UserId} IssuerPresent={IssuerPresent} SubjectPresent={SubjectPresent}",
                    resolution?.Strategy.ToString() ?? ExternalIdentityClaims.ExternalIdentityUserIdStrategy.MissingClaims.ToString(),
                    ExternalIdentityClaims.MaskUserId(userId),
                    !string.IsNullOrWhiteSpace(resolution?.Issuer),
                    !string.IsNullOrWhiteSpace(resolution?.Subject));
            }

            return userId;
        }
    }
}
