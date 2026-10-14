using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using Contracts.Common.Messages;
using Shared.Paging;

namespace AerationSterilize.Application.Features.V1.Products.Queries.GetProductById;
public record GetProductByIdQuery(Guid Id) : IQuery<ProductDto>;


