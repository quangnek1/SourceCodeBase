using CommonServices.Application.Common.Validators;
using FluentValidation;

namespace CommonServices.Application.Features.V1.Banks.Commands.CreateBank;
internal sealed class CreateBankCommandValidator : AbstractValidator<CreateBankCommand>
{
    public CreateBankCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(ValidatorExtensions.CodeMaxLength);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(ValidatorExtensions.NameMaxLength);

        RuleFor(x => x.VAPrefix)
            .MaximumLength(50);

        RuleFor(x => x.Translations)
            .MustHaveUniqueLangCodes();

        RuleForEach(x => x.Translations)
            .SetValidator(new TranslationRequestValidator());
    }
}
