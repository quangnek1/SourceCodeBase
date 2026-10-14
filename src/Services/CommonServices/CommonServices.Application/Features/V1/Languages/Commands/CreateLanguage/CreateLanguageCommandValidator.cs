using CommonServices.Application.Common.Validators;
using CommonServices.Application.Features.V1.Languages.Common.Validators;
using FluentValidation;

namespace CommonServices.Application.Features.V1.Languages.Commands.CreateLanguage;
internal sealed class CreateLanguageCommandValidator : AbstractValidator<CreateLanguageCommand>
{
    public CreateLanguageCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(ValidatorExtensions.LangCodeMaxLength); // the code is stored as LangCode in every translation table

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(256);

        RuleFor(x => x.FlagImagePath)
            .MaximumLength(1000);

        RuleFor(x => x.Platform)
            .IsInEnum();

        RuleFor(x => x.Translations)
            .MustHaveUniqueLangCodes();

        RuleForEach(x => x.Translations)
            .SetValidator(new LanguageTranslationRequestValidator());
    }
}
