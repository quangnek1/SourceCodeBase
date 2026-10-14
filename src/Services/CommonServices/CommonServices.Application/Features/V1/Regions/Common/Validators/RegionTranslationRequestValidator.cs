using CommonServices.Application.Common.Validators;
using CommonServices.Application.Features.V1.Regions.Common.Dtos;
using FluentValidation;

namespace CommonServices.Application.Features.V1.Regions.Common.Validators;
public sealed class RegionTranslationRequestValidator : AbstractValidator<RegionTranslationRequest>
{
    public RegionTranslationRequestValidator()
    {
        RuleFor(x => x.LangCode)
            .NotEmpty()
            .MaximumLength(ValidatorExtensions.LangCodeMaxLength);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(ValidatorExtensions.NameMaxLength);

        RuleFor(x => x.Note)
            .MaximumLength(500);
    }
}
