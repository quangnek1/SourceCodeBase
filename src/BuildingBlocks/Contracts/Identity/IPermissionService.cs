namespace Contracts.Identity;
public interface IPermissionService
{
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
