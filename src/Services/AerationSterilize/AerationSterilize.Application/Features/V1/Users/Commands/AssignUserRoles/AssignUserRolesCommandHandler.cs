using AerationSterilize.Domain.Entities.Identity;
using Contracts.Common.Messages;
using Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static AerationSterilize.Domain.Exceptions.RoleException;
using static AerationSterilize.Domain.Exceptions.UserException;

namespace AerationSterilize.Application.Features.V1.Users.Commands.AssignUserRoles;
internal sealed class AssignUserRolesCommandHandler : ICommandHandler<AssignUserRolesCommand>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;

    public AssignUserRolesCommandHandler(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
    }

    public async Task<Result> Handle(AssignUserRolesCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString())
            ?? throw new UserNotFoundException(request.UserId);

        var roleIds = request.RoleIds.Distinct().ToList();
        var roles = await _roleManager.Roles
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => new { r.Id, r.Name })
            .ToListAsync(cancellationToken);

        var missingRoleId = roleIds.FirstOrDefault(id => roles.All(r => r.Id != id));
        if (missingRoleId != Guid.Empty)
            throw new RoleNotFoundException(missingRoleId);

        var wantedRoles = roles.Select(r => r.Name!).ToList();
        var currentRoles = await _userManager.GetRolesAsync(user);

        var toRemove = currentRoles.Except(wantedRoles, StringComparer.OrdinalIgnoreCase).ToList();
        var toAdd = wantedRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();

        if (toRemove.Count > 0)
            EnsureSucceeded(await _userManager.RemoveFromRolesAsync(user, toRemove));

        if (toAdd.Count > 0)
            EnsureSucceeded(await _userManager.AddToRolesAsync(user, toAdd));

        return Result.Success();
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new UserOperationFailedException(string.Join(", ", result.Errors.Select(e => e.Description)));
    }
}
