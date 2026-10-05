using Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Constants.Authorization;

namespace AerationSterilize.Persistence.Services;
public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _context;

    public PermissionService(ApplicationDbContext context)
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // AspNetUserRoles (UserId, RoleId)  JOIN  Permissions (RoleId, FunctionId, ActionId)
        var query =
            from ur in _context.UserRoles
            join p in _context.Permissions on ur.RoleId equals p.RoleId
            join f in _context.Functions on p.FunctionId equals f.Id
            join a in _context.Actions on p.ActionId equals a.Id
            where ur.UserId == userId
                  && f.IsActive == true
                  && a.IsActive == true
            select new { p.FunctionId, p.ActionId };

        var rows = await query.Distinct().ToListAsync(cancellationToken);

        return rows.Select(x => PermissionClaim.Build(x.FunctionId, x.ActionId)).ToList();
    }
}
