using CommonServices.Application.Common.Validators;
using FluentValidation;

namespace CommonServices.Application.Features.V1.Currencies.Commands.SetCurrencyStatus;
internal sealed class SetCurrencyStatusCommandValidator : AbstractValidator<SetCurrencyStatusCommand>
{
    public SetCurrencyStatusCommandValidator()
    {
        RuleFor(x => x.Status)
            .MustBeActiveOrInactive();
    }
}
