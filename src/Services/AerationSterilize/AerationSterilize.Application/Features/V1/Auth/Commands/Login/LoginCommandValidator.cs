using FluentValidation;

namespace AerationSterilize.Application.Features.V1.Auth.Commands.Login;
internal class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
