using AerationSterilize.Domain.Entities.Identity;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;

namespace AerationSterilize.Application.Features.V1.Auth.Commands.Logout;
public class LogoutCommandHanlder : ICommandHandler<LogoutCommand>
{
    private readonly IRepositoryBase<UserSession, int> _userSessionRepository;
    public LogoutCommandHanlder(IRepositoryBase<UserSession, int> userSessionRepository)
    {
        _userSessionRepository = userSessionRepository ?? throw new ArgumentNullException(nameof(userSessionRepository));
    }
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenEntity = await _userSessionRepository
            .FindSingleAsync(us => us.RefreshToken == request.RefreshToken, cancellationToken)
         ?? throw new Exception("Invalid refresh token");

        tokenEntity.IsRevoked = true;

        return Result.Success();
    }
}
