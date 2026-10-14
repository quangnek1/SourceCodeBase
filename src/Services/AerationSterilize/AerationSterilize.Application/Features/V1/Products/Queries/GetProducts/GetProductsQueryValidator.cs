using FluentValidation;

namespace AerationSterilize.Application.Features.V1.Products.Queries.GetProducts;
public class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsQueryValidator()
    {
        RuleFor(x => x.sortColumn)
           .NotEmpty()
           .WithMessage("Sort column is required.");
    }
}
