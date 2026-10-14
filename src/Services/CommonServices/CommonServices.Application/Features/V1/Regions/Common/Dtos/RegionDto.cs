using CommonServices.Domain.Enums;

namespace CommonServices.Application.Features.V1.Regions.Common.Dtos;

// Name and Note are already translated into the requested language.
public record RegionDto(
    Guid Id,
    string? Code,
    string Name,
    string? Note,
    int SortOrder,
    ObjectStatus Status);
