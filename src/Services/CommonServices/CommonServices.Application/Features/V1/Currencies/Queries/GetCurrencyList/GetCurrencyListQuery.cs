using CommonServices.Application.Features.V1.Currencies.Common.Caching;
using CommonServices.Application.Features.V1.Currencies.Common.Dtos;
using CommonServices.Domain.Enums;
using Contracts.Caching;

namespace CommonServices.Application.Features.V1.Currencies.Queries.GetCurrencyList;

// Whole list (no paging) for drop-downs. Status = null returns every currency that is not deleted.
public sealed record GetCurrencyListQuery(string LangCode, ObjectStatus? Status) : ICachedQuery<IReadOnlyList<CurrencyDto>>
{
    string ICachedQuery.CacheKey => CurrencyCacheKeys.List(LangCode, Status);

    TimeSpan? ICachedQuery.Expiration => null;
}
