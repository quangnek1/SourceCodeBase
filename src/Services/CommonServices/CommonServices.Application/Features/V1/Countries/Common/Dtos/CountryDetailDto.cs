using CommonServices.Application.Common.Dtos;
using CommonServices.Domain.Enums;

namespace CommonServices.Application.Features.V1.Countries.Common.Dtos;

// Used by the edit screen: default-language values plus every translation.
public record CountryDetailDto(
    Guid Id,
    string Code,
    string Name,
    Guid? RegionId,
    Guid? CurrencyId,
    ObjectStatus Status,
    DateTimeOffset CreatedDate,
    DateTimeOffset? LastModifiedDate,
    IReadOnlyList<TranslationDto> Translations);
