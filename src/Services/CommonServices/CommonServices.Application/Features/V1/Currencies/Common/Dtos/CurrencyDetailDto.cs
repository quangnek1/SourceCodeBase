using CommonServices.Application.Common.Dtos;
using CommonServices.Domain.Enums;

namespace CommonServices.Application.Features.V1.Currencies.Common.Dtos;

// Used by the edit screen: default-language values plus every translation.
public record CurrencyDetailDto(
    Guid Id,
    string Code,
    string Name,
    string? Symbol,
    bool IsCurrencyOptionEnabled,
    ObjectStatus Status,
    DateTimeOffset CreatedDate,
    DateTimeOffset? LastModifiedDate,
    IReadOnlyList<TranslationDto> Translations);
