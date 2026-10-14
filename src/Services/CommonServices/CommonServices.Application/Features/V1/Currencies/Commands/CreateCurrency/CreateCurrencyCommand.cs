using CommonServices.Application.Common.Dtos;
using CommonServices.Application.Features.V1.Currencies.Common.Caching;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Currencies.Commands.CreateCurrency;
public sealed record CreateCurrencyCommand(
    string Code,
    string Name,
    string? Symbol,
    bool IsCurrencyOptionEnabled,
    IReadOnlyList<TranslationRequest>? Translations
) : ICommand<Guid>, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CurrencyCacheKeys.Prefix];
}
