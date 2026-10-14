using CommonServices.Application.Features.V1.Cities.Common.Caching;
using CommonServices.Application.Features.V1.Cities.Common.Dtos;
using CommonServices.Domain.Enums;
using Contracts.Caching;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.Application.Features.V1.Cities.Queries.GetCities;
public sealed record GetCitiesQuery(
    string? SearchTerm,
    ObjectStatus? Status,
    Guid? CountryId,
    string LangCode,
    string? SortColumn,
    SortOrder? SortOrder,
    int PageIndex,
    int PageSize)
    : ICachedQuery<PagedResult<CityDto>>
{
    string ICachedQuery.CacheKey => CityCacheKeys.Page(SearchTerm, Status, CountryId, LangCode, SortColumn, SortOrder?.Name, PageIndex, PageSize);

    TimeSpan? ICachedQuery.Expiration => TimeSpan.FromMinutes(5);
}
