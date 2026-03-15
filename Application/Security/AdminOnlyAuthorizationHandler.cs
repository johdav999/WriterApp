using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace WriterApp.Application.Security
{
    public sealed class AdminOnlyRequirement : IAuthorizationRequirement
    {
    }

    public sealed class AdminOnlyAuthorizationHandler : AuthorizationHandler<AdminOnlyRequirement>
    {
        private readonly IAdminAccessResolver _adminAccessResolver;

        public AdminOnlyAuthorizationHandler(IAdminAccessResolver adminAccessResolver)
        {
            _adminAccessResolver = adminAccessResolver ?? throw new ArgumentNullException(nameof(adminAccessResolver));
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminOnlyRequirement requirement)
        {
            AdminAccessDiagnosticInfo diagnostic = _adminAccessResolver.Describe(context.User);

            AdminPolicyDiagnostics.LogDecision(
                diagnostic.IsRoleAdmin,
                diagnostic.BootstrapEnabled,
                diagnostic.BootstrapOidConfigured,
                diagnostic.UserOidPresent,
                diagnostic.Resolution.IsAdminAccess,
                bootstrapOid: null,
                userOid: ExternalIdentityClaims.ResolveOid(context.User.Claims));

            if (diagnostic.Resolution.Source == AdminAccessSource.Bootstrap)
            {
                AdminPolicyDiagnostics.LogBootstrapAccessGranted(context.Resource, ExternalIdentityClaims.ResolveOid(context.User.Claims));
            }

            if (diagnostic.Resolution.IsAdminAccess)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
