using CommonServices.Application.Common;
using static CommonServices.Domain.Exceptions.AuthException;
using CommonServices.Domain.Entities.Identity;
using Contracts.Caching;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Identity;
using Contracts.Responses;

namespace CommonServices.Application.Features.V1.Auth.Commands.Logout;
public class LogoutCommandHandler : ICommandHandler<LogoutCommand>
{
    private readonly IRepositoryBase<UserSession, int> _userSessionRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ITokenRevocationService _tokenRevocationService;
    private readonly ICacheService _cacheService;

    public LogoutCommandHandler(IRepositoryBase<UserSession, int> userSessionRepository,
        ICurrentUser currentUser,
        ITokenRevocationService tokenRevocationService,
        ICacheService cacheService)
    {
        _userSessionRepository = userSessionRepository ?? throw new ArgumentNullException(nameof(userSessionRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _tokenRevocationService = tokenRevocationService ?? throw new ArgumentNullException(nameof(tokenRevocationService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenEntity = await _userSessionRepository
            .FindSingleAsync(us => us.RefreshToken == request.RefreshToken, cancellationToken)
         ?? throw new InvalidRefreshTokenException();

        tokenEntity.IsRevoked = true;

        // Revoke the access token used for this request, so it stops working now instead of at expiry.
        if (_currentUser.TokenId is { } jti && _currentUser.TokenExpiresAt is { } expiresAt)
            await _tokenRevocationService.RevokeAsync(jti, expiresAt, cancellationToken);

        await _cacheService.RemoveAsync(CacheKeys.Permissions(tokenEntity.UserId), cancellationToken);

        return Result.Success();
    }
}
