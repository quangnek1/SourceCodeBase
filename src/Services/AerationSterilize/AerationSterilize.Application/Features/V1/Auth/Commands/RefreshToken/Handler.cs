using AerationSterilize.Application.Features.V1.Auth.Common.Dtos;
using AerationSterilize.Domain.Entities.Identity;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Identity;
using Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using Shared.DTOs.Identity;

namespace AerationSterilize.Application.Features.V1.Auth.Commands.RefreshToken;
public class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, LoginDto>
{
    private readonly IRepositoryBase<UserSession, int> _userSessionRepository;
    private readonly UserManager<AppUser> _userManager;
    private readonly ITokenService _tokenService;

    public RefreshTokenCommandHandler(IRepositoryBase<UserSession, int> userSessionRepository,
       ITokenService tokenService,
       UserManager<AppUser> userManager)
    {
        _userSessionRepository = userSessionRepository ?? throw new ArgumentNullException(nameof(userSessionRepository));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }

    public async Task<Result<LoginDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenEntity = await _userSessionRepository.FindSingleAsync(us => us.RefreshToken == request.RefreshToken, cancellationToken)
            ?? throw new Exception("Invalid refresh token");

        var user = await _userManager.FindByIdAsync(tokenEntity.UserId.ToString());

        // revoke token cũ
        tokenEntity.IsRevoked = true;

        var roles = await _userManager.GetRolesAsync(tokenEntity.User);

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


