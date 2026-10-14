using CommonServices.Application.Features.V1.Banks.Common.Caching;
using CommonServices.Application.Features.V1.Banks.Common.Dtos;
using CommonServices.Domain.Enums;
using Contracts.Caching;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.Application.Features.V1.Banks.Queries.GetBanks;
public sealed record GetBanksQuery(
    string? SearchTerm,
    ObjectStatus? Status,
    string LangCode,
    string? SortColumn,
    SortOrder? SortOrder,
    int PageIndex,
    int PageSize)
    : ICachedQuery<PagedResult<BankDto>>
{
    string ICachedQuery.CacheKey => BankCacheKeys.Page(SearchTerm, Status, LangCode, SortColumn, SortOrder?.Name, PageIndex, PageSize);

    TimeSpan? ICachedQuery.Expiration => TimeSpan.FromMinutes(5);
}
