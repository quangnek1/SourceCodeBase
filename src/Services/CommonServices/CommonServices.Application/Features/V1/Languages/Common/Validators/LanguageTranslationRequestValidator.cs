using CommonServices.Application.Common.Validators;
using CommonServices.Application.Features.V1.Languages.Common.Dtos;
using FluentValidation;

namespace CommonServices.Application.Features.V1.Languages.Common.Validators;
public sealed class LanguageTranslationRequestValidator : AbstractValidator<LanguageTranslationRequest>
{
    public LanguageTranslationRequestValidator()
    {
        RuleFor(x => x.LangCode)
            .NotEmpty()
            .MaximumLength(ValidatorExtensions.LangCodeMaxLength);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(256);
    }
}
