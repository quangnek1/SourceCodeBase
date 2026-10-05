using Shared.DTOs.Identity;

namespace Contracts.Identity;
public interface IPermissionService
{
    Task<IReadOnlyList<UserPermission>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
