using CommonServices.Application.Common.Validators;
using FluentValidation;

namespace CommonServices.Application.Features.V1.Banks.Queries.GetBanks;
public class GetBanksQueryValidator : AbstractValidator<GetBanksQuery>
{
    public GetBanksQueryValidator()
    {
        RuleFor(x => x.PageIndex)
            .MustBeValidPageIndex();

        RuleFor(x => x.PageSize)
            .MustBeValidPageSize();
    }
}
