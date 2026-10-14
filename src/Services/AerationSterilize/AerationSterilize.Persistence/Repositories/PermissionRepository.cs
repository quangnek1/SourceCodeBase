using AerationSterilize.Domain.Entities.Identity;
using AerationSterilize.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Action = AerationSterilize.Domain.Entities.Identity.Action;

namespace AerationSterilize.Persistence.Repositories;
internal sealed class PermissionRepository : IPermissionRepository
{
    private readonly ApplicationDbContext _context;

    public PermissionRepository(ApplicationDbContext context)
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<IReadOnlyList<Function>> GetActiveFunctionsAsync(CancellationToken cancellationToken = default)
        => await _context.Functions
            .AsNoTracking()
            .Where(f => f.IsActive == true)
            .OrderBy(f => f.SortOrder)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Action>> GetActiveActionsAsync(CancellationToken cancellationToken = default)
        => await _context.Actions
            .AsNoTracking()
            .Where(a => a.IsActive == true)
            .OrderBy(a => a.SortOrder)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ActionInFunction>> GetActionInFunctionsAsync(CancellationToken cancellationToken = default)
        => await _context.ActionInFunctions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Permission>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
        => await _context.Permissions
            .AsNoTracking()
            .Where(p => p.RoleId == roleId)
            .ToListAsync(cancellationToken);

    public async Task ReplaceRolePermissionsAsync(Guid roleId, IReadOnlyCollection<Permission> permissions, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Permissions
            .Where(p => p.RoleId == roleId)
            .ToListAsync(cancellationToken);

        // Apply the difference instead of delete-all + insert-all:
        // EF cannot track a new entity while a deleted one with the same key is still tracked.
        var wanted = permissions.ToDictionary(p => (p.FunctionId, p.ActionId));
        foreach (var current in existing)
        {
            if (wanted.Remove((current.FunctionId, current.ActionId), out var updated))
                current.AccessLevel = updated.AccessLevel;
            else
                _context.Permissions.Remove(current);
        }

        await _context.Permissions.AddRangeAsync(wanted.Values, cancellationToken);
    }

    public Task<bool> RoleHasUsersAsync(Guid roleId, CancellationToken cancellationToken = default)
        => _context.UserRoles.AnyAsync(ur => ur.RoleId == roleId, cancellationToken);
}
