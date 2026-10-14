using CommonServices.Application.Common.Dtos;
using CommonServices.Domain.Enums;

namespace CommonServices.Application.Features.V1.Banks.Common.Dtos;

// Used by the edit screen: default-language values plus every translation.
public record BankDetailDto(
    Guid Id,
    string Code,
    string Name,
    string? VAPrefix,
    bool IsVAByBank,
    ObjectStatus Status,
    DateTimeOffset CreatedDate,
    DateTimeOffset? LastModifiedDate,
    IReadOnlyList<TranslationDto> Translations);
