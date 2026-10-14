using CommonServices.Application.Features.V1.Cities.Common.Caching;
using CommonServices.Application.Features.V1.Cities.Common.Dtos;
using CommonServices.Domain.Enums;
using Contracts.Caching;

namespace CommonServices.Application.Features.V1.Cities.Queries.GetCityList;

// Whole list (no paging) for drop-downs, optionally limited to some countries.
// Status = null returns every city that is not deleted.
public sealed record GetCityListQuery(string LangCode, ObjectStatus? Status, IReadOnlyCollection<Guid>? CountryIds)
    : ICachedQuery<IReadOnlyList<CityDto>>
{
    string ICachedQuery.CacheKey => CityCacheKeys.List(LangCode, Status, CountryIds);

    TimeSpan? ICachedQuery.Expiration => null;
}
