using AerationSterilize.Domain.Entities.Identity;
using AerationSterilize.Domain.Repositories;
using Contracts.Common.Messages;
using Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using static AerationSterilize.Domain.Exceptions.RoleException;

namespace AerationSterilize.Application.Features.V1.Roles.Commands.DeleteRole;
internal sealed class DeleteRoleCommandHandler : ICommandHandler<DeleteRoleCommand>
{
    private readonly RoleManager<AppRole> _roleManager;
    private readonly IPermissionRepository _permissionRepository;

    public DeleteRoleCommandHandler(RoleManager<AppRole> roleManager, IPermissionRepository permissionRepository)
    {
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _permissionRepository = permissionRepository ?? throw new ArgumentNullException(nameof(permissionRepository));
    }

    public async Task<Result> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleManager.FindByIdAsync(request.Id.ToString())
            ?? throw new RoleNotFoundException(request.Id);

        // Unassign the role from its users first, so nobody loses access by surprise.
        if (await _permissionRepository.RoleHasUsersAsync(role.Id, cancellationToken))
            throw new RoleInUseException(role.Name!);

        var result = await _roleManager.DeleteAsync(role); // the role's permissions are removed by cascade delete
        if (!result.Succeeded)
            throw new RoleOperationFailedException(string.Join(", ", result.Errors.Select(e => e.Description)));

        return Result.Success();
    }
}
