using CommonServices.Application.Common.Dtos;
using CommonServices.Application.Features.V1.Cities.Common.Caching;
using CommonServices.Application.Features.V1.Countries.Common.Caching;
using CommonServices.Application.Features.V1.Countries.Common.Dtos;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Countries.Commands.UpdateCountry;
public sealed class UpdateCountryCommand : ICommand<CountryDetailDto>, ICacheInvalidator
{
    public Guid Id { get; private set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Guid? RegionId { get; set; }
    public Guid? CurrencyId { get; set; }
    public IReadOnlyList<TranslationRequest>? Translations { get; set; }

    // Cities show the country name, so their cached entries are cleared too.
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CountryCacheKeys.Prefix, CityCacheKeys.Prefix];

    public void SetId(Guid id)
    {
        Id = id;
    }
}
