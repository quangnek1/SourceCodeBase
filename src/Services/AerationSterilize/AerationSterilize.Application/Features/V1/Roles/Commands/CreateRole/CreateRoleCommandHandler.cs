using AerationSterilize.Domain.Entities.Identity;
using Contracts.Common.Messages;
using Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using static AerationSterilize.Domain.Exceptions.RoleException;

namespace AerationSterilize.Application.Features.V1.Roles.Commands.CreateRole;
internal sealed class CreateRoleCommandHandler : ICommandHandler<CreateRoleCommand, Guid>
{
    private readonly RoleManager<AppRole> _roleManager;

    public CreateRoleCommandHandler(RoleManager<AppRole> roleManager)
    {
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
    }

    public async Task<Result<Guid>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        if (await _roleManager.RoleExistsAsync(request.Name))
            throw new RoleAlreadyExistsException(request.Name);

        var role = new AppRole
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            RoleCode = request.RoleCode,
            Description = request.Description ?? string.Empty
        };

        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded)
            throw new RoleOperationFailedException(string.Join(", ", result.Errors.Select(e => e.Description)));

        return Result.Success(role.Id);
    }
}
