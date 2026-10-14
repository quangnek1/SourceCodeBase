using CommonServices.Application.Features.V1.Languages.Common.Caching;
using CommonServices.Application.Features.V1.Languages.Common.Dtos;
using CommonServices.Domain.Enums;
using Contracts.Caching;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.Application.Features.V1.Languages.Queries.GetLanguages;
public sealed record GetLanguagesQuery(
    string? SearchTerm,
    ObjectStatus? Status,
    string LangCode,
    string? SortColumn,
    SortOrder? SortOrder,
    int PageIndex,
    int PageSize)
    : ICachedQuery<PagedResult<LanguageDto>>
{
    string ICachedQuery.CacheKey => LanguageCacheKeys.Page(SearchTerm, Status, LangCode, SortColumn, SortOrder?.Name, PageIndex, PageSize);

    TimeSpan? ICachedQuery.Expiration => TimeSpan.FromMinutes(5);
}
