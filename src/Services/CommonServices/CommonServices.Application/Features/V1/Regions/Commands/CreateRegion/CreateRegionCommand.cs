using CommonServices.Application.Features.V1.Regions.Common.Caching;
using CommonServices.Application.Features.V1.Regions.Common.Dtos;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Regions.Commands.CreateRegion;
public sealed record CreateRegionCommand(
    string? Code,
    string Name,
    string? Note,
    int SortOrder,
    IReadOnlyList<RegionTranslationRequest>? Translations
) : ICommand<Guid>, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [RegionCacheKeys.Prefix];
}
