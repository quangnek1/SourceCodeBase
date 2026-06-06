namespace AerationSterilize.Application.Features.V1.Products.Common.Dtos;
public record ProductDto(
    Guid Id,
    string Name,
    decimal Price,
    string Description);

