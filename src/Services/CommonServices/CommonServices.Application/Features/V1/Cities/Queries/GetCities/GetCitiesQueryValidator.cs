using CommonServices.Application.Common.Validators;
using FluentValidation;

namespace CommonServices.Application.Features.V1.Cities.Queries.GetCities;
public class GetCitiesQueryValidator : AbstractValidator<GetCitiesQuery>
{
    public GetCitiesQueryValidator()
    {
        RuleFor(x => x.PageIndex)
            .MustBeValidPageIndex();

        RuleFor(x => x.PageSize)
            .MustBeValidPageSize();
    }
}
