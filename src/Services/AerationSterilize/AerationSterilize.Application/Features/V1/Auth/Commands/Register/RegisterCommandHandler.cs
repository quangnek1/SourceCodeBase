using AerationSterilize.Domain.Entities.Identity;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Identity;
using Contracts.Responses;
using Microsoft.AspNetCore.Identity;

namespace AerationSterilize.Application.Features.V1.Auth.Commands.Register;
internal class RegisterCommandHandler : ICommandHandler<RegisterCommand>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IRepositoryBase<UserSession, int> _userSessionRepository;

    public RegisterCommandHandler(UserManager<AppUser> userManager, ITokenService tokenService,
          IRepositoryBase<UserSession, int> userSessionRepository)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _userSessionRepository = userSessionRepository ?? throw new ArgumentNullException(nameof(userSessionRepository));
    }

    public async Task<Result> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await _userManager.FindByNameAsync(request.UserName) is not null)
        {
            return Result.Failure(new Error("USERNAME_EXISTS", "Username already exists"));
        }

        if (await _userManager.FindByEmailAsync(request.Email) is not null)
        {
            return Result.Failure(new Error("EMAIL_EXISTS", "Email already exists"));
        }

        var userEntity = new AppUser
        {
            UserName = request.UserName,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            FullName = request.FullName
        };

        var result = await _userManager.CreateAsync(userEntity, request.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(x => x.Description));

            throw new Exception(errors);
        }

        return Result.Success();
    }
}
