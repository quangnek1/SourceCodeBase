using CommonServices.Application.Common.Dtos;
using CommonServices.Application.Features.V1.Countries.Common.Caching;
using CommonServices.Application.Features.V1.Currencies.Common.Caching;
using CommonServices.Application.Features.V1.Currencies.Common.Dtos;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Currencies.Commands.UpdateCurrency;
public sealed class UpdateCurrencyCommand : ICommand<CurrencyDetailDto>, ICacheInvalidator
{
    public Guid Id { get; private set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Symbol { get; set; }
    public bool IsCurrencyOptionEnabled { get; set; }
    public IReadOnlyList<TranslationRequest>? Translations { get; set; }

    // Countries show the currency code, so their cached entries are cleared too.
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CurrencyCacheKeys.Prefix, CountryCacheKeys.Prefix];

    public void SetId(Guid id)
    {
        Id = id;
    }
}
