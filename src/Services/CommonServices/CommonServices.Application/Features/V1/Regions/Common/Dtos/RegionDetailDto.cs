using CommonServices.Domain.Enums;

namespace CommonServices.Application.Features.V1.Regions.Common.Dtos;

// Used by the edit screen: default-language values plus every translation.
public record RegionDetailDto(
    Guid Id,
    string? Code,
    string Name,
    string? Note,
    int SortOrder,
    ObjectStatus Status,
    DateTimeOffset CreatedDate,
    DateTimeOffset? LastModifiedDate,
    IReadOnlyList<RegionTranslationDto> Translations);

public sealed record RegionTranslationDto(string LangCode, string Name, string? Note);
