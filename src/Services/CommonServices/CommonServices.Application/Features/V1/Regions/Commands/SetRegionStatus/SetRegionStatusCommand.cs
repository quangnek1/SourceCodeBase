using CommonServices.Application.Features.V1.Countries.Common.Caching;
using CommonServices.Application.Features.V1.Regions.Common.Caching;
using CommonServices.Domain.Enums;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Regions.Commands.SetRegionStatus;
public sealed record SetRegionStatusCommand(Guid Id, ObjectStatus Status) : ICommand, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [RegionCacheKeys.Prefix, CountryCacheKeys.Prefix];
}
