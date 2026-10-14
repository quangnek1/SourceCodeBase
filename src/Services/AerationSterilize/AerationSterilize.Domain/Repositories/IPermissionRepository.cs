using AerationSterilize.Domain.Entities.Identity;
using Action = AerationSterilize.Domain.Entities.Identity.Action;

namespace AerationSterilize.Domain.Repositories;

// Function, Action and Permission have no EntityBase key, so they don't fit IRepositoryBase.
// Changes are saved by the unit of work in TransactionPipelineBehavior.
public interface IPermissionRepository
{
    Task<IReadOnlyList<Function>> GetActiveFunctionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Action>> GetActiveActionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActionInFunction>> GetActionInFunctionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Permission>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task ReplaceRolePermissionsAsync(Guid roleId, IReadOnlyCollection<Permission> permissions, CancellationToken cancellationToken = default);

    Task<bool> RoleHasUsersAsync(Guid roleId, CancellationToken cancellationToken = default);
}
