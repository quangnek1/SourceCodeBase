using CommonServices.Domain.Enums;

namespace CommonServices.Application.Features.V1.Languages.Common.Dtos;

// Name and Description are already translated into the requested language.
public record LanguageDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? FlagImagePath,
    LanguagePlatform? Platform,
    ObjectStatus Status);
