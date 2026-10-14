using AerationSterilize.Application.Features.V1.Roles.Common.Dtos;
using AerationSterilize.Domain.Entities.Identity;
using Contracts.Common.Messages;
using Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AerationSterilize.Application.Features.V1.Roles.Queries.GetRoles;
internal sealed class GetRolesQueryHandler : IQueryHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    private readonly RoleManager<AppRole> _roleManager;

    public GetRolesQueryHandler(RoleManager<AppRole> roleManager)
    {
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
    }

    public async Task<Result<IReadOnlyList<RoleDto>>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = await _roleManager.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name!, r.RoleCode, r.Description))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<RoleDto>>(roles);
    }
}
