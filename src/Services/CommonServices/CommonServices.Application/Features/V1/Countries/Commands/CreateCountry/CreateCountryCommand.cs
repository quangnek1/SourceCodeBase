using CommonServices.Application.Common.Dtos;
using CommonServices.Application.Features.V1.Countries.Common.Caching;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Countries.Commands.CreateCountry;
public sealed record CreateCountryCommand(
    string Code,
    string Name,
    Guid? RegionId,
    Guid? CurrencyId,
    IReadOnlyList<TranslationRequest>? Translations
) : ICommand<Guid>, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CountryCacheKeys.Prefix];
}
