using CommonServices.Domain.Enums;

namespace CommonServices.Application.Features.V1.Cities.Common.Dtos;

// Name and CountryName are already translated into the requested language.
public record CityDto(
    Guid Id,
    string Code,
    string Name,
    Guid CountryId,
    string CountryCode,
    string CountryName,
    ObjectStatus Status);
