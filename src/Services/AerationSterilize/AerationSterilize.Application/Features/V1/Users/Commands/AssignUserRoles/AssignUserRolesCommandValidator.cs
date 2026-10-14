using FluentValidation;

namespace AerationSterilize.Application.Features.V1.Users.Commands.AssignUserRoles;
internal sealed class AssignUserRolesCommandValidator : AbstractValidator<AssignUserRolesCommand>
{
    public AssignUserRolesCommandValidator()
    {
        RuleFor(x => x.RoleIds)
            .NotNull();

        RuleForEach(x => x.RoleIds)
            .NotEmpty();
    }
}
