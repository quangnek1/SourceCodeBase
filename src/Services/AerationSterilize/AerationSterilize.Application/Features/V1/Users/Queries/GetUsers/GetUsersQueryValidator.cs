using FluentValidation;
using Shared.Paging;

namespace AerationSterilize.Application.Features.V1.Users.Queries.GetUsers;
internal sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(x => x.PageIndex)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PagedResult<object>.UpperPageSize);
    }
}
