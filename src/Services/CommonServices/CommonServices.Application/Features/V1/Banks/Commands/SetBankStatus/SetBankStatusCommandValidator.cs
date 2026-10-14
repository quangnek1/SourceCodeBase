using CommonServices.Application.Common.Validators;
using FluentValidation;

namespace CommonServices.Application.Features.V1.Banks.Commands.SetBankStatus;
internal sealed class SetBankStatusCommandValidator : AbstractValidator<SetBankStatusCommand>
{
    public SetBankStatusCommandValidator()
    {
        RuleFor(x => x.Status)
            .MustBeActiveOrInactive();
    }
}
