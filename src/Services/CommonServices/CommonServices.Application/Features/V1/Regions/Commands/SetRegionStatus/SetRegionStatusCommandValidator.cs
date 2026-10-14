using CommonServices.Application.Common.Validators;
using FluentValidation;

namespace CommonServices.Application.Features.V1.Regions.Commands.SetRegionStatus;
internal sealed class SetRegionStatusCommandValidator : AbstractValidator<SetRegionStatusCommand>
{
    public SetRegionStatusCommandValidator()
    {
        RuleFor(x => x.Status)
            .MustBeActiveOrInactive();
    }
}
