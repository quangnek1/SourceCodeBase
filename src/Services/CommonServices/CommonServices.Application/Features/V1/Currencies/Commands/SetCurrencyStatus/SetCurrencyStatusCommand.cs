using CommonServices.Application.Features.V1.Countries.Common.Caching;
using CommonServices.Application.Features.V1.Currencies.Common.Caching;
using CommonServices.Domain.Enums;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Currencies.Commands.SetCurrencyStatus;
public sealed record SetCurrencyStatusCommand(Guid Id, ObjectStatus Status) : ICommand, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CurrencyCacheKeys.Prefix, CountryCacheKeys.Prefix];
}
