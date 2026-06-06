namespace Shared.DTOs.Identity;
public sealed record TokenRequest(
    Guid UserId,
    string UserName,
    IList<string> Roles);
