using CommonServices.Application.Features.V1.Languages.Common.Caching;
using CommonServices.Application.Features.V1.Languages.Common.Dtos;
using CommonServices.Domain.Enums;
using Contracts.Caching;

namespace CommonServices.Application.Features.V1.Languages.Queries.GetLanguageList;

// Whole list (no paging). Status = null returns every language that is not deleted.
public sealed record GetLanguageListQuery(string LangCode, ObjectStatus? Status) : ICachedQuery<IReadOnlyList<LanguageDto>>
{
    string ICachedQuery.CacheKey => LanguageCacheKeys.List(LangCode, Status);

    TimeSpan? ICachedQuery.Expiration => null;
}
