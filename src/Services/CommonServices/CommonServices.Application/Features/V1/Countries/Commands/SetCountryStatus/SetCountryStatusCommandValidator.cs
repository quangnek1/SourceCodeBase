using CommonServices.Application.Common.Validators;
using FluentValidation;

namespace CommonServices.Application.Features.V1.Countries.Commands.SetCountryStatus;
internal sealed class SetCountryStatusCommandValidator : AbstractValidator<SetCountryStatusCommand>
{
    public SetCountryStatusCommandValidator()
    {
        RuleFor(x => x.Status)
            .MustBeActiveOrInactive();
    }
}
