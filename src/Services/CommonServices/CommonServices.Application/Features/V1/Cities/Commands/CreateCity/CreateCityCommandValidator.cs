using CommonServices.Application.Common.Validators;
using FluentValidation;

namespace CommonServices.Application.Features.V1.Cities.Commands.CreateCity;
internal sealed class CreateCityCommandValidator : AbstractValidator<CreateCityCommand>
{
    public CreateCityCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(ValidatorExtensions.CodeMaxLength);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(ValidatorExtensions.NameMaxLength);

        RuleFor(x => x.CountryId)
            .NotEmpty();

        RuleFor(x => x.Translations)
            .MustHaveUniqueLangCodes();

        RuleForEach(x => x.Translations)
            .SetValidator(new TranslationRequestValidator());
    }
}
