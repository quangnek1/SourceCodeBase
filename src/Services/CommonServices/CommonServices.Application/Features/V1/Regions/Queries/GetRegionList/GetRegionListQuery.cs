using CommonServices.Application.Features.V1.Regions.Common.Caching;
using CommonServices.Application.Features.V1.Regions.Common.Dtos;
using CommonServices.Domain.Enums;
using Contracts.Caching;

namespace CommonServices.Application.Features.V1.Regions.Queries.GetRegionList;

// Whole list (no paging) for drop-downs, ordered by SortOrder. Status = null returns every region that is not deleted.
public sealed record GetRegionListQuery(string LangCode, ObjectStatus? Status) : ICachedQuery<IReadOnlyList<RegionDto>>
{
    string ICachedQuery.CacheKey => RegionCacheKeys.List(LangCode, Status);

    TimeSpan? ICachedQuery.Expiration => null;
}
