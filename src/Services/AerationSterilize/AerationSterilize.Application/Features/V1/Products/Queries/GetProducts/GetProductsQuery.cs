using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using AerationSterilize.Application.Features.V1.Products.Common.Caching;
using Contracts.Caching;
using Shared.Emumerations;
using Shared.Paging;

namespace AerationSterilize.Application.Features.V1.Products.Queries.GetProducts;
public sealed record GetProductsQuery(
    string? searchTerm,
    string? sortColumn,
    SortOrder? SortOrder,
    int PageIndex, int PageSize)
     : ICachedQuery<PagedResult<ProductDto>>
{
    string ICachedQuery.CacheKey => ProductCacheKeys.List(searchTerm, sortColumn, SortOrder?.Name, PageIndex, PageSize);

    TimeSpan? ICachedQuery.Expiration => TimeSpan.FromMinutes(5);
}
