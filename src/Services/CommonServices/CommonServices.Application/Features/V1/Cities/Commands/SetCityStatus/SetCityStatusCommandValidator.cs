using CommonServices.Application.Common.Validators;
using FluentValidation;

namespace CommonServices.Application.Features.V1.Cities.Commands.SetCityStatus;
internal sealed class SetCityStatusCommandValidator : AbstractValidator<SetCityStatusCommand>
{
    public SetCityStatusCommandValidator()
    {
        RuleFor(x => x.Status)
            .MustBeActiveOrInactive();
    }
}
