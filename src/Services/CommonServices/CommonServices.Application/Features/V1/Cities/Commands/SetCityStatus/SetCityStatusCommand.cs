using CommonServices.Application.Features.V1.Cities.Common.Caching;
using CommonServices.Domain.Enums;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Cities.Commands.SetCityStatus;
public sealed record SetCityStatusCommand(Guid Id, ObjectStatus Status) : ICommand, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CityCacheKeys.Prefix];
}
