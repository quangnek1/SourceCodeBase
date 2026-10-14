using CommonServices.Application.Features.V1.Countries.Common.Caching;
using CommonServices.Application.Features.V1.Currencies.Common.Caching;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Currencies.Commands.DeleteCurrency;
public sealed record DeleteCurrencyCommand(Guid Id) : ICommand, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CurrencyCacheKeys.Prefix, CountryCacheKeys.Prefix];
}
