using AerationSterilize.Application.Common;
using AerationSterilize.Application.Features.V1.Auth.Common.Dtos;
using AerationSterilize.Domain.Entities.Identity;
using Contracts.Common.Messages;
using Contracts.Identity;
using Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using static AerationSterilize.Domain.Exceptions.UserException;

namespace AerationSterilize.Application.Features.V1.Auth.Queries.GetMe;
internal sealed class GetMeQueryHandler : IQueryHandler<GetMeQuery, MeDto>
{
    private readonly ICurrentUser _currentUser;
    private readonly UserManager<AppUser> _userManager;
    private readonly IPermissionService _permissionService;

    public GetMeQueryHandler(ICurrentUser currentUser, UserManager<AppUser> userManager, IPermissionService permissionService)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
    }

    public async Task<Result<MeDto>> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new UserNotFoundException(userId);

        var roles = await _userManager.GetRolesAsync(user);

        // Read from the permission cache/DB rather than the token, so the result reflects the latest matrix.
        var permissions = await _permissionService.GetPermissionsAsync(user.Id, cancellationToken);

        return Result.Success(new MeDto(user.Id, user.UserName!, user.FullName, user.Email, roles.ToList(), permissions));
    }
}
