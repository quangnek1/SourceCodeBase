using CommonServices.Application.Common.Validators;
using FluentValidation;

namespace CommonServices.Application.Features.V1.Countries.Commands.CreateCountry;
internal sealed class CreateCountryCommandValidator : AbstractValidator<CreateCountryCommand>
{
    public CreateCountryCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(ValidatorExtensions.CodeMaxLength);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(ValidatorExtensions.NameMaxLength);

        RuleFor(x => x.Translations)
            .MustHaveUniqueLangCodes();

        RuleForEach(x => x.Translations)
            .SetValidator(new TranslationRequestValidator());
    }
}
