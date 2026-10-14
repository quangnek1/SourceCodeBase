using CommonServices.Application.Features.V1.Countries.Common.Caching;
using CommonServices.Application.Features.V1.Regions.Common.Caching;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Regions.Commands.DeleteRegion;
public sealed record DeleteRegionCommand(Guid Id) : ICommand, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [RegionCacheKeys.Prefix, CountryCacheKeys.Prefix];
}
