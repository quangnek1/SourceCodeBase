using AerationSterilize.Application.Features.V1.Permissions.Common.Dtos;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Permissions.Commands.UpdateRolePermissions;

// Replaces the whole matrix of a role: the cells sent are the ticked ones, everything else is removed.
public sealed class UpdateRolePermissionsCommand : ICommand, ICacheInvalidator
{
    public Guid RoleId { get; private set; }
    public List<RolePermissionDto> Permissions { get; set; } = [];

    // Users of this role may also hold other roles, and their cached permissions mix all of them,
    // so clear every user's cached permissions. They are rebuilt on the next login / token refresh.
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CacheKeys.PermissionPrefix];

    public void SetRoleId(Guid roleId)
    {
        RoleId = roleId;
    }
}
