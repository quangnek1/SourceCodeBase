using Microsoft.AspNetCore.Authorization;
using Shared.Common.Constants.Authorization;

namespace Infrastructure.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // A permission granted with AccessLevel.None gives no access to any data, so it does not open the endpoint either.
        var levelPrefix = requirement.Value + ":";
        var hasAccess = context.User.FindAll(PermissionClaim.LevelType)
            .Any(c => c.Value.StartsWith(levelPrefix, StringComparison.Ordinal)
                      && int.TryParse(c.Value[levelPrefix.Length..], out var level)
                      && level > (int)AccessLevel.None);

        if (context.User.HasClaim(PermissionClaim.Type, requirement.Value) && hasAccess)
            context.Succeed(requirement);

        // Không Succeed => ASP.NET tự trả 403 (đã login) hoặc 401 (chưa login)
        return Task.CompletedTask;
    }
}
