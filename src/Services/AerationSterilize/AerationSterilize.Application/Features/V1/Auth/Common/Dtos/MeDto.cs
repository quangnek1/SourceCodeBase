using Shared.DTOs.Identity;

namespace AerationSterilize.Application.Features.V1.Auth.Common.Dtos;

// What the front end needs to show or hide menus and buttons for the signed-in user.
public sealed record MeDto(
    Guid Id,
    string UserName,
    string? FullName,
    string? Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<UserPermission> Permissions);
