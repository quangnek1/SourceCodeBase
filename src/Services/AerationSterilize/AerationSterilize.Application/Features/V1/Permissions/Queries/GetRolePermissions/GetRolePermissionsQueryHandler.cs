using AerationSterilize.Application.Features.V1.Permissions.Common.Dtos;
using AerationSterilize.Domain.Entities.Identity;
using AerationSterilize.Domain.Repositories;
using Contracts.Common.Messages;
using Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using Shared.Common.Constants.Authorization;
using static AerationSterilize.Domain.Exceptions.RoleException;

namespace AerationSterilize.Application.Features.V1.Permissions.Queries.GetRolePermissions;
internal sealed class GetRolePermissionsQueryHandler : IQueryHandler<GetRolePermissionsQuery, IReadOnlyList<RolePermissionDto>>
{
    private readonly RoleManager<AppRole> _roleManager;
    private readonly IPermissionRepository _permissionRepository;

    public GetRolePermissionsQueryHandler(RoleManager<AppRole> roleManager, IPermissionRepository permissionRepository)
    {
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _permissionRepository = permissionRepository ?? throw new ArgumentNullException(nameof(permissionRepository));
    }

    public async Task<Result<IReadOnlyList<RolePermissionDto>>> Handle(GetRolePermissionsQuery request, CancellationToken cancellationToken)
    {
        if (await _roleManager.FindByIdAsync(request.RoleId.ToString()) is null)
            throw new RoleNotFoundException(request.RoleId);

        var permissions = await _permissionRepository.GetByRoleAsync(request.RoleId, cancellationToken);

        var result = permissions
            .Where(p => p.AccessLevel != AccessLevel.None)
            .Select(p => new RolePermissionDto(p.FunctionId, p.ActionId, p.AccessLevel))
            .ToList();

        return Result.Success<IReadOnlyList<RolePermissionDto>>(result);
    }
}
