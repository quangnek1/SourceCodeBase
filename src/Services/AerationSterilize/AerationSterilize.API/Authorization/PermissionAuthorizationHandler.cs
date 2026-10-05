using Microsoft.AspNetCore.Authorization;
using Shared.Common.Constants.Authorization;

namespace AerationSterilize.API.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim(PermissionClaim.Type, requirement.Value))
            context.Succeed(requirement);

        // Không Succeed => ASP.NET tự trả 403 (đã login) hoặc 401 (chưa login)
        return Task.CompletedTask;
    }
}
