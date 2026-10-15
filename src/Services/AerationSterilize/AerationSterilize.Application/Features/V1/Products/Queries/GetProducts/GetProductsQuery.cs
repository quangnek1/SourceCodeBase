using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using AerationSterilize.Application.Features.V1.Products.Common.Caching;
using Contracts.Caching;
using Contracts.Identity;
using Shared.Emumerations;
using Shared.Paging;

namespace AerationSterilize.Application.Features.V1.Products.Queries.GetProducts;
public sealed record GetProductsQuery(
    string? searchTerm,
    string? sortColumn,
    SortOrder? SortOrder,
    int PageIndex, int PageSize,
    DataScope Scope)
     : ICachedQuery<PagedResult<ProductDto>>
{
    string ICachedQuery.CacheKey => ProductCacheKeys.List(Scope.CacheKey, searchTerm, sortColumn, SortOrder?.Name, PageIndex, PageSize);

    TimeSpan? ICachedQuery.Expiration => TimeSpan.FromMinutes(5);
}
