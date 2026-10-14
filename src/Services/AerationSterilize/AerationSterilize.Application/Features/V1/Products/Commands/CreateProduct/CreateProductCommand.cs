using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Products.Commands.CreateProduct;
public sealed record CreateProductCommand(
    string Name,
    decimal Price,
    string Description
) : ICommand<Guid>;
