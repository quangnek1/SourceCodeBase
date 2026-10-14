using CommonServices.Domain.Enums;

namespace CommonServices.Application.Features.V1.Languages.Common.Dtos;

// Used by the edit screen: default-language values plus every translation.
public record LanguageDetailDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? FlagImagePath,
    LanguagePlatform? Platform,
    ObjectStatus Status,
    DateTimeOffset CreatedDate,
    DateTimeOffset? LastModifiedDate,
    IReadOnlyList<LanguageTranslationDto> Translations);

public sealed record LanguageTranslationDto(string LangCode, string Name, string? Description);
