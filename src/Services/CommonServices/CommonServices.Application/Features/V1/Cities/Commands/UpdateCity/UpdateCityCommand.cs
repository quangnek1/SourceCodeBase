using CommonServices.Application.Common.Dtos;
using CommonServices.Application.Features.V1.Cities.Common.Caching;
using CommonServices.Application.Features.V1.Cities.Common.Dtos;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Cities.Commands.UpdateCity;
public sealed class UpdateCityCommand : ICommand<CityDetailDto>, ICacheInvalidator
{
    public Guid Id { get; private set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Guid CountryId { get; set; }
    public IReadOnlyList<TranslationRequest>? Translations { get; set; }

    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CityCacheKeys.Prefix];

    public void SetId(Guid id)
    {
        Id = id;
    }
}
