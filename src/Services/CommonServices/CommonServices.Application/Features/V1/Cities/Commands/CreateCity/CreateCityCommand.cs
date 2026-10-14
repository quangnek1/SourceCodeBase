using CommonServices.Application.Common.Dtos;
using CommonServices.Application.Features.V1.Cities.Common.Caching;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Cities.Commands.CreateCity;
public sealed record CreateCityCommand(
    string Code,
    string Name,
    Guid CountryId,
    IReadOnlyList<TranslationRequest>? Translations
) : ICommand<Guid>, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CityCacheKeys.Prefix];
}
