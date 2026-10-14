using AerationSterilize.Domain.Entities.Identity;
using AerationSterilize.Domain.Repositories;
using Contracts.Common.Messages;
using Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using Shared.Common.Constants.Authorization;
using static AerationSterilize.Domain.Exceptions.PermissionException;
using static AerationSterilize.Domain.Exceptions.RoleException;

namespace AerationSterilize.Application.Features.V1.Permissions.Commands.UpdateRolePermissions;
internal sealed class UpdateRolePermissionsCommandHandler : ICommandHandler<UpdateRolePermissionsCommand>
{
    private readonly RoleManager<AppRole> _roleManager;
    private readonly IPermissionRepository _permissionRepository;

    public UpdateRolePermissionsCommandHandler(RoleManager<AppRole> roleManager, IPermissionRepository permissionRepository)
    {
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _permissionRepository = permissionRepository ?? throw new ArgumentNullException(nameof(permissionRepository));
    }

    public async Task<Result> Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        if (await _roleManager.FindByIdAsync(request.RoleId.ToString()) is null)
            throw new RoleNotFoundException(request.RoleId);

        // AccessLevel.None means "not ticked", which is stored as no row.
        // A cell sent twice keeps the widest access level.
        var granted = request.Permissions
            .Where(p => p.AccessLevel != AccessLevel.None)
            .GroupBy(p => (p.FunctionId, p.ActionId))
            .Select(g => new Permission
            {
                RoleId = request.RoleId,
                FunctionId = g.Key.FunctionId,
                ActionId = g.Key.ActionId,
                AccessLevel = g.Max(p => p.AccessLevel)
            })
            .ToList();

        await EnsureCellsExistAsync(granted, cancellationToken);

        await _permissionRepository.ReplaceRolePermissionsAsync(request.RoleId, granted, cancellationToken);

        return Result.Success();
    }

    // Only cells shown in the matrix can be ticked: an active function, an active action, and listed in ActionInFunction.
    private async Task EnsureCellsExistAsync(IReadOnlyCollection<Permission> granted, CancellationToken cancellationToken)
    {
        var functionIds = (await _permissionRepository.GetActiveFunctionsAsync(cancellationToken)).Select(f => f.Id).ToHashSet();
        var actionIds = (await _permissionRepository.GetActiveActionsAsync(cancellationToken)).Select(a => a.Id).ToHashSet();
        var validCells = (await _permissionRepository.GetActionInFunctionsAsync(cancellationToken))
            .Where(aif => functionIds.Contains(aif.FunctionId) && actionIds.Contains(aif.ActionId))
            .Select(aif => (aif.FunctionId, aif.ActionId))
            .ToHashSet();

        var invalidCodes = granted
            .Where(p => !validCells.Contains((p.FunctionId, p.ActionId)))
            .Select(p => PermissionClaim.Build(p.FunctionId, p.ActionId))
            .ToList();

        if (invalidCodes.Count > 0)
            throw new InvalidPermissionException(invalidCodes);
    }
}
