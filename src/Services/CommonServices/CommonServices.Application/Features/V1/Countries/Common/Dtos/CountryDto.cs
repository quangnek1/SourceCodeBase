using CommonServices.Domain.Enums;

namespace CommonServices.Application.Features.V1.Countries.Common.Dtos;

// Name and RegionName are already translated into the requested language.
public record CountryDto(
    Guid Id,
    string Code,
    string Name,
    Guid? RegionId,
    string? RegionName,
    Guid? CurrencyId,
    string? CurrencyCode,
    ObjectStatus Status);
