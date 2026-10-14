using CommonServices.Application.Features.V1.Regions.Common.Caching;
using CommonServices.Application.Features.V1.Regions.Common.Dtos;
using Contracts.Caching;

namespace CommonServices.Application.Features.V1.Regions.Queries.GetRegionById;
public sealed record GetRegionByIdQuery(Guid Id) : ICachedQuery<RegionDetailDto>
{
    string ICachedQuery.CacheKey => RegionCacheKeys.ById(Id);

    TimeSpan? ICachedQuery.Expiration => null;
}
