using FluentValidation;

namespace AerationSterilize.Application.Features.V1.Permissions.Commands.UpdateRolePermissions;
internal sealed class UpdateRolePermissionsCommandValidator : AbstractValidator<UpdateRolePermissionsCommand>
{
    public UpdateRolePermissionsCommandValidator()
    {
        RuleFor(x => x.Permissions)
            .NotNull();

        RuleForEach(x => x.Permissions).ChildRules(permission =>
        {
            permission.RuleFor(p => p.FunctionId).NotEmpty();
            permission.RuleFor(p => p.ActionId).NotEmpty();
            permission.RuleFor(p => p.AccessLevel).IsInEnum();
        });
    }
}
