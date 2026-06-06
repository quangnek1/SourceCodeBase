using AerationSterilize.Application.Features.V1.Auth.Common.Dtos;
using AerationSterilize.Domain.Entities.Identity;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Identity;
using Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using Shared.DTOs.Identity;

namespace AerationSterilize.Application.Features.V1.Auth.Commands.Login;
internal class LoginCommandHandler : ICommandHandler<LoginCommand, LoginDto>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IRepositoryBase<UserSession, int> _userSessionRepository;

    public LoginCommandHandler(UserManager<AppUser> userManager, ITokenService tokenService,
          IRepositoryBase<UserSession, int> userSessionRepository)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _userSessionRepository = userSessionRepository ?? throw new ArgumentNullException(nameof(userSessionRepository));
    }

    public async Task<Result<LoginDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByNameAsync(request.UserName) ?? throw new Exception("Invalid account");

        var isValidPassword = await _userManager.CheckPasswordAsync(user, request.Password);

        if (!isValidPassword)
            throw new Exception("Invalid account");

        var roles = await _userManager.GetRolesAsync(user);

        var tokenRequest = new TokenRequest(user.Id, user.UserName!, roles);
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
