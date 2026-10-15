using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using AerationSterilize.Application.Features.V1.Products.Common.Caching;
using Contracts.Caching;
using Contracts.Identity;

namespace AerationSterilize.Application.Features.V1.Products.Queries.GetProductById;
public sealed record GetProductByIdQuery(Guid Id, DataScope Scope) : ICachedQuery<ProductDto>
{
    string ICachedQuery.CacheKey => ProductCacheKeys.ById(Id, Scope.CacheKey);

    TimeSpan? ICachedQuery.Expiration => null;
}
