using AerationSterilize.Domain.Entities.Identity;
using Contracts.Common.Messages;
using Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using static AerationSterilize.Domain.Exceptions.RoleException;

namespace AerationSterilize.Application.Features.V1.Roles.Commands.UpdateRole;
internal sealed class UpdateRoleCommandHandler : ICommandHandler<UpdateRoleCommand>
{
    private readonly RoleManager<AppRole> _roleManager;

    public UpdateRoleCommandHandler(RoleManager<AppRole> roleManager)
    {
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
    }

    public async Task<Result> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleManager.FindByIdAsync(request.Id.ToString())
            ?? throw new RoleNotFoundException(request.Id);

        var nameChanged = !string.Equals(role.Name, request.Name, StringComparison.OrdinalIgnoreCase);
        if (nameChanged && await _roleManager.RoleExistsAsync(request.Name))
            throw new RoleAlreadyExistsException(request.Name);

        role.Name = request.Name;
        role.RoleCode = request.RoleCode;
        role.Description = request.Description ?? string.Empty;

        var result = await _roleManager.UpdateAsync(role);
        if (!result.Succeeded)
            throw new RoleOperationFailedException(string.Join(", ", result.Errors.Select(e => e.Description)));

        return Result.Success();
    }
}
