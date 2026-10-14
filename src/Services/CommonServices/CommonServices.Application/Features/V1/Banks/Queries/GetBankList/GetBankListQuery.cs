using CommonServices.Application.Features.V1.Banks.Common.Caching;
using CommonServices.Application.Features.V1.Banks.Common.Dtos;
using CommonServices.Domain.Enums;
using Contracts.Caching;

namespace CommonServices.Application.Features.V1.Banks.Queries.GetBankList;

// Whole list (no paging) for drop-downs. Status = null returns every bank that is not deleted.
public sealed record GetBankListQuery(string LangCode, ObjectStatus? Status) : ICachedQuery<IReadOnlyList<BankDto>>
{
    string ICachedQuery.CacheKey => BankCacheKeys.List(LangCode, Status);

    TimeSpan? ICachedQuery.Expiration => null;
}
