using CommonServices.Domain.Enums;

namespace CommonServices.Application.Features.V1.Banks.Common.Dtos;

// Name is already translated into the requested language.
public record BankDto(
    Guid Id,
    string Code,
    string Name,
    string? VAPrefix,
    bool IsVAByBank,
    ObjectStatus Status);
