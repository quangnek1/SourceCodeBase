using CommonServices.Application.Common.Dtos;
using FluentValidation;

namespace CommonServices.Application.Common.Validators;
public sealed class TranslationRequestValidator : AbstractValidator<TranslationRequest>
{
    public TranslationRequestValidator()
    {
        RuleFor(x => x.LangCode)
            .NotEmpty()
            .MaximumLength(ValidatorExtensions.LangCodeMaxLength);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(ValidatorExtensions.NameMaxLength);
    }
}
