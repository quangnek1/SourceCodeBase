namespace AerationSterilize.Application.Features.V1.Users.Common.Dtos;
public sealed record UserSummaryDto(
    Guid Id,
    string UserName,
    string? Email,
    string? FullName,
    IReadOnlyList<Guid> RoleIds);
