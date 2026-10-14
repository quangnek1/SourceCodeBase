using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Products.Commands.UpdateProduct;
public sealed class UpdateProductCommand : ICommand<ProductDto>
{
    public Guid Id { get; private set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }

    public void SetId(Guid id)
    {
        Id = id;
    }
}

