using CommonServices.Application.Common.Validators;
using FluentValidation;

namespace CommonServices.Application.Features.V1.Languages.Queries.GetLanguages;
public class GetLanguagesQueryValidator : AbstractValidator<GetLanguagesQuery>
{
    public GetLanguagesQueryValidator()
    {
        RuleFor(x => x.PageIndex)
            .MustBeValidPageIndex();

        RuleFor(x => x.PageSize)
            .MustBeValidPageSize();
    }
}
