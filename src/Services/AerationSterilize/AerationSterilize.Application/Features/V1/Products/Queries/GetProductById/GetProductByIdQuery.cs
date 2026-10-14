using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using AerationSterilize.Application.Features.V1.Products.Common.Caching;
using Contracts.Caching;

namespace AerationSterilize.Application.Features.V1.Products.Queries.GetProductById;
public sealed record GetProductByIdQuery(Guid Id) : ICachedQuery<ProductDto>
{
    string ICachedQuery.CacheKey => ProductCacheKeys.ById(Id);

    TimeSpan? ICachedQuery.Expiration => null;
}
