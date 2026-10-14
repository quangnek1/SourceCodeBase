using CommonServices.Application.Features.V1.Currencies.Common.Caching;
using CommonServices.Application.Features.V1.Currencies.Common.Dtos;
using Contracts.Caching;

namespace CommonServices.Application.Features.V1.Currencies.Queries.GetCurrencyById;
public sealed record GetCurrencyByIdQuery(Guid Id) : ICachedQuery<CurrencyDetailDto>
{
    string ICachedQuery.CacheKey => CurrencyCacheKeys.ById(Id);

    TimeSpan? ICachedQuery.Expiration => null;
}
