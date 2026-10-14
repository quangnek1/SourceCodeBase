using CommonServices.Application.Features.V1.Cities.Common.Caching;
using CommonServices.Application.Features.V1.Countries.Common.Caching;
using CommonServices.Domain.Enums;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Countries.Commands.SetCountryStatus;
public sealed record SetCountryStatusCommand(Guid Id, ObjectStatus Status) : ICommand, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CountryCacheKeys.Prefix, CityCacheKeys.Prefix];
}
