using CommonServices.Application.Features.V1.Languages.Common.Caching;
using CommonServices.Application.Features.V1.Languages.Common.Dtos;
using Contracts.Caching;

namespace CommonServices.Application.Features.V1.Languages.Queries.GetNativeLanguages;

// Active languages, each named in itself (legacy "languages/native").
public sealed record GetNativeLanguagesQuery : ICachedQuery<IReadOnlyList<LanguageDto>>
{
    string ICachedQuery.CacheKey => LanguageCacheKeys.Native;

    TimeSpan? ICachedQuery.Expiration => null;
}
