using Contracts.Caching;
using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Users.Commands.AssignUserRoles;

// Replaces the user's roles with RoleIds.
public sealed class AssignUserRolesCommand : ICommand, ICacheInvalidator
{
    public Guid UserId { get; private set; }
    public List<Guid> RoleIds { get; set; } = [];

    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CacheKeys.Permissions(UserId)];

    public void SetUserId(Guid userId)
    {
        UserId = userId;
    }
}
