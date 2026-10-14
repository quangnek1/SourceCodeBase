using CommonServices.Application.Features.V1.Languages.Common.Caching;
using CommonServices.Application.Features.V1.Languages.Common.Dtos;
using Contracts.Caching;

namespace CommonServices.Application.Features.V1.Languages.Queries.GetLanguageById;
public sealed record GetLanguageByIdQuery(Guid Id) : ICachedQuery<LanguageDetailDto>
{
    string ICachedQuery.CacheKey => LanguageCacheKeys.ById(Id);

    TimeSpan? ICachedQuery.Expiration => null;
}
