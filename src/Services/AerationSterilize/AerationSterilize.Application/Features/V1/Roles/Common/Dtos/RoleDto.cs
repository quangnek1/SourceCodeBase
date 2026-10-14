namespace AerationSterilize.Application.Features.V1.Roles.Common.Dtos;
public sealed record RoleDto(
    Guid Id,
    string Name,
    string RoleCode,
    string Description);
