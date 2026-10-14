using CommonServices.Application.Features.V1.Countries.Common.Caching;
using CommonServices.Application.Features.V1.Countries.Common.Dtos;
using CommonServices.Domain.Enums;
using Contracts.Caching;

namespace CommonServices.Application.Features.V1.Countries.Queries.GetCountryList;

// Whole list (no paging) for drop-downs, optionally limited to some regions.
// Status = null returns every country that is not deleted.
public sealed record GetCountryListQuery(string LangCode, ObjectStatus? Status, IReadOnlyCollection<Guid>? RegionIds)
    : ICachedQuery<IReadOnlyList<CountryDto>>
{
    string ICachedQuery.CacheKey => CountryCacheKeys.List(LangCode, Status, RegionIds);

    TimeSpan? ICachedQuery.Expiration => null;
}
