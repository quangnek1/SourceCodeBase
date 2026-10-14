using CommonServices.Application.Features.V1.Countries.Common.Caching;
using CommonServices.Application.Features.V1.Countries.Common.Dtos;
using CommonServices.Domain.Enums;
using Contracts.Caching;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.Application.Features.V1.Countries.Queries.GetCountries;
public sealed record GetCountriesQuery(
    string? SearchTerm,
    ObjectStatus? Status,
    Guid? RegionId,
    string LangCode,
    string? SortColumn,
    SortOrder? SortOrder,
    int PageIndex,
    int PageSize)
    : ICachedQuery<PagedResult<CountryDto>>
{
    string ICachedQuery.CacheKey => CountryCacheKeys.Page(SearchTerm, Status, RegionId, LangCode, SortColumn, SortOrder?.Name, PageIndex, PageSize);

    TimeSpan? ICachedQuery.Expiration => TimeSpan.FromMinutes(5);
}
