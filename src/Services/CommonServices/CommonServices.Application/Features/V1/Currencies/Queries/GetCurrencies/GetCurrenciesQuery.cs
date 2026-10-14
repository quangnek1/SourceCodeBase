using CommonServices.Application.Features.V1.Currencies.Common.Caching;
using CommonServices.Application.Features.V1.Currencies.Common.Dtos;
using CommonServices.Domain.Enums;
using Contracts.Caching;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.Application.Features.V1.Currencies.Queries.GetCurrencies;
public sealed record GetCurrenciesQuery(
    string? SearchTerm,
    ObjectStatus? Status,
    string LangCode,
    string? SortColumn,
    SortOrder? SortOrder,
    int PageIndex,
    int PageSize)
    : ICachedQuery<PagedResult<CurrencyDto>>
{
    string ICachedQuery.CacheKey => CurrencyCacheKeys.Page(SearchTerm, Status, LangCode, SortColumn, SortOrder?.Name, PageIndex, PageSize);

    TimeSpan? ICachedQuery.Expiration => TimeSpan.FromMinutes(5);
}
