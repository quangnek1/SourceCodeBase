using CommonServices.Application.Common.Validators;
using FluentValidation;

namespace CommonServices.Application.Features.V1.Currencies.Queries.GetCurrencies;
public class GetCurrenciesQueryValidator : AbstractValidator<GetCurrenciesQuery>
{
    public GetCurrenciesQueryValidator()
    {
        RuleFor(x => x.PageIndex)
            .MustBeValidPageIndex();

        RuleFor(x => x.PageSize)
            .MustBeValidPageSize();
    }
}
