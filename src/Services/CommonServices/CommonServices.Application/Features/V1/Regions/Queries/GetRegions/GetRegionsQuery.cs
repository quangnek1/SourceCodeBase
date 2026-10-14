using CommonServices.Application.Features.V1.Regions.Common.Caching;
using CommonServices.Application.Features.V1.Regions.Common.Dtos;
using CommonServices.Domain.Enums;
using Contracts.Caching;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.Application.Features.V1.Regions.Queries.GetRegions;
public sealed record GetRegionsQuery(
    string? SearchTerm,
    ObjectStatus? Status,
    string LangCode,
    string? SortColumn,
    SortOrder? SortOrder,
    int PageIndex,
    int PageSize)
    : ICachedQuery<PagedResult<RegionDto>>
{
    string ICachedQuery.CacheKey => RegionCacheKeys.Page(SearchTerm, Status, LangCode, SortColumn, SortOrder?.Name, PageIndex, PageSize);

    TimeSpan? ICachedQuery.Expiration => TimeSpan.FromMinutes(5);
}
