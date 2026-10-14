using CommonServices.Application.Common.Dtos;
using CommonServices.Domain.Enums;

namespace CommonServices.Application.Features.V1.Cities.Common.Dtos;

// Used by the edit screen: default-language values plus every translation.
public record CityDetailDto(
    Guid Id,
    string Code,
    string Name,
    Guid CountryId,
    ObjectStatus Status,
    DateTimeOffset CreatedDate,
    DateTimeOffset? LastModifiedDate,
    IReadOnlyList<TranslationDto> Translations);
