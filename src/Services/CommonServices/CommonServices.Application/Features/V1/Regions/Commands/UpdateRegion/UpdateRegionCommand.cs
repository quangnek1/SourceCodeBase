using CommonServices.Application.Features.V1.Countries.Common.Caching;
using CommonServices.Application.Features.V1.Regions.Common.Caching;
using CommonServices.Application.Features.V1.Regions.Common.Dtos;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Regions.Commands.UpdateRegion;
public sealed class UpdateRegionCommand : ICommand<RegionDetailDto>, ICacheInvalidator
{
    public Guid Id { get; private set; }
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public string? Note { get; set; }
    public int SortOrder { get; set; }
    public IReadOnlyList<RegionTranslationRequest>? Translations { get; set; }

    // Countries show the region name, so their cached entries are cleared too.
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [RegionCacheKeys.Prefix, CountryCacheKeys.Prefix];

    public void SetId(Guid id)
    {
        Id = id;
    }
}
