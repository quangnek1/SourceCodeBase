using Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Constants.Authorization;
using Shared.DTOs.Identity;

namespace AerationSterilize.Persistence.Services;
public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _context;

    public PermissionService(ApplicationDbContext context)
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<IReadOnlyList<UserPermission>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // AspNetUserRoles (UserId, RoleId)  JOIN  Permissions (RoleId, FunctionId, ActionId)
        var rows = await (
            from ur in _context.UserRoles
            join p in _context.Permissions on ur.RoleId equals p.RoleId
            join f in _context.Functions on p.FunctionId equals f.Id
            join a in _context.Actions on p.ActionId equals a.Id
            where ur.UserId == userId && f.IsActive == true && a.IsActive == true
            select new { p.FunctionId, p.ActionId, p.AccessLevel })
        .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new { x.FunctionId, x.ActionId })
            .Select(g => new UserPermission(
                PermissionClaim.Build(g.Key.FunctionId, g.Key.ActionId),
                g.Max(x => x.AccessLevel)))
            .ToList();
    }
}

