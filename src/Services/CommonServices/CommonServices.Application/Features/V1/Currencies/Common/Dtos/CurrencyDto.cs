using CommonServices.Domain.Enums;

namespace CommonServices.Application.Features.V1.Currencies.Common.Dtos;

// Name is already translated into the requested language.
public record CurrencyDto(
    Guid Id,
    string Code,
    string Name,
    string? Symbol,
    bool IsCurrencyOptionEnabled,
    ObjectStatus Status);
