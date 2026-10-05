using System.Security.Claims;
using AerationSterilize.Application.Features.V1.Auth.Common.Dtos;
using AerationSterilize.Domain.Entities.Identity;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Identity;
using Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Identity;

namespace AerationSterilize.Application.Features.V1.Auth.Commands.Login;
internal class LoginCommandHandler : ICommandHandler<LoginCommand, LoginDto>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;
    private readonly IPermissionService _permissionService;

    private readonly ITokenService _tokenService;
    private readonly IRepositoryBase<UserSession, int> _userSessionRepository;

    public LoginCommandHandler(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager, ITokenService tokenService,
          IRepositoryBase<UserSession, int> userSessionRepository, IPermissionService permissionService)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _userSessionRepository = userSessionRepository ?? throw new ArgumentNullException(nameof(userSessionRepository));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
    }

    public async Task<Result<LoginDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByNameAsync(request.UserName) ?? throw new Exception("Invalid account");

        var isValidPassword = await _userManager.CheckPasswordAsync(user, request.Password);

        if (!isValidPassword)
            throw new Exception("Invalid account");


        var results = new Dictionary<string, int>();

        var roleNames = await _userManager.GetRolesAsync(user);
        var permissions = await _permissionService.GetPermissionsAsync(user.Id, cancellationToken);

        //var roles = await _roleManager.Roles.Where(p => roleNames.Contains(p.Name)).ToListAsync();

        //var roleClaims = new List<Claim>();
        //foreach (var role in roles)
        //{
        //    var resultClaims = await _roleManager.GetClaimsAsync(role);
        //    if (resultClaims.Any())
        //    {
        //        roleClaims.AddRange(resultClaims);
        //    }
        //}

        //var roleClaimNames = roleClaims.Select(x => x.Type).Distinct();
        //foreach (var name in roleClaimNames)
        //{
        //    if (!results.ContainsKey(name))
        //    {
        //        var claims = roleClaims.Where(p => p.Type == name);
        //        if (!claims.Any(p => p.Value == "-1"))
        //        {
        //            var value = 0;
        //            foreach (var claim in claims)
        //            {
        //                if (int.TryParse(claim.Value, out int claimValue))
        //                    value |= claimValue;
        //            }
        //            results.Add(name, value);
        //        }
        //    }
        //}


        var tokenRequest = new TokenRequest(user.Id, user.UserName!, roleNames, permissions);
        var token = _tokenService.GetToken(tokenRequest);

        var userSession = new UserSession
        {
            UserId = user.Id,
            RefreshToken = token.RefreshToken,
            ExpiredAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn),
        };
        _userSessionRepository.Add(userSession);

        var result = new LoginDto(
           token.AccessToken,
           token.RefreshToken,
           token.ExpiresIn
        );

        return Result.Success(result);
    }
}
