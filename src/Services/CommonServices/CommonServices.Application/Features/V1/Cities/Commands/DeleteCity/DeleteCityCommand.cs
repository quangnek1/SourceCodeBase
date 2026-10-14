using CommonServices.Application.Features.V1.Cities.Common.Caching;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Cities.Commands.DeleteCity;
public sealed record DeleteCityCommand(Guid Id) : ICommand, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CityCacheKeys.Prefix];
}
