using CommonServices.Application.Common.Validators;
using FluentValidation;

namespace CommonServices.Application.Features.V1.Languages.Commands.SetLanguageStatus;
internal sealed class SetLanguageStatusCommandValidator : AbstractValidator<SetLanguageStatusCommand>
{
    public SetLanguageStatusCommandValidator()
    {
        RuleFor(x => x.Status)
            .MustBeActiveOrInactive();
    }
}
