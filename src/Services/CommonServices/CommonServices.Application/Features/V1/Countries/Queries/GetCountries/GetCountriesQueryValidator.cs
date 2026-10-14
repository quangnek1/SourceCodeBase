using CommonServices.Application.Common.Validators;
using FluentValidation;

namespace CommonServices.Application.Features.V1.Countries.Queries.GetCountries;
public class GetCountriesQueryValidator : AbstractValidator<GetCountriesQuery>
{
    public GetCountriesQueryValidator()
    {
        RuleFor(x => x.PageIndex)
            .MustBeValidPageIndex();

        RuleFor(x => x.PageSize)
            .MustBeValidPageSize();
    }
}
