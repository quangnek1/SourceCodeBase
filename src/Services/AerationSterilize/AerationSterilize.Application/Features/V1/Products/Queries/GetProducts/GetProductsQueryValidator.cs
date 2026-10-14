using FluentValidation;

namespace AerationSterilize.Application.Features.V1.Products.Queries.GetProducts;
public class GetProductsValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsValidator()
    {
        RuleFor(x => x.sortColumn)
           .NotEmpty()
           .WithMessage("Sort column is required.");
    }
}
