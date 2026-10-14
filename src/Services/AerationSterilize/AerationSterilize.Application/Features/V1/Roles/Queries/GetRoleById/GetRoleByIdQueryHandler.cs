using AerationSterilize.Application.Features.V1.Roles.Common.Dtos;
using AerationSterilize.Domain.Entities.Identity;
using Contracts.Common.Messages;
using Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using static AerationSterilize.Domain.Exceptions.RoleException;

namespace AerationSterilize.Application.Features.V1.Roles.Queries.GetRoleById;
internal sealed class GetRoleByIdQueryHandler : IQueryHandler<GetRoleByIdQuery, RoleDto>
{
    private readonly RoleManager<AppRole> _roleManager;

    public GetRoleByIdQueryHandler(RoleManager<AppRole> roleManager)
    {
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
    }

    public async Task<Result<RoleDto>> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var role = await _roleManager.FindByIdAsync(request.Id.ToString())
            ?? throw new RoleNotFoundException(request.Id);

        return Result.Success(new RoleDto(role.Id, role.Name!, role.RoleCode, role.Description));
    }
}
