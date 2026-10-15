namespace Shared.DTOs.Identity;
public sealed record TokenRequest(
    Guid UserId,
    string UserName,
    Guid? DepartmentId,
    IList<string> Roles,
    IReadOnlyList<UserPermission> Permissions);
