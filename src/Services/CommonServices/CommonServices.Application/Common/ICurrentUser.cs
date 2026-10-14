using Shared.Common.Constants.Authorization;

namespace CommonServices.Application.Common;
public interface ICurrentUser
{
    Guid UserId { get; }
    Guid? DepartmentId { get; }
    string? TokenId { get; }
    DateTimeOffset? TokenExpiresAt { get; }
    AccessLevel GetAccessLevel(string functionCode, string commandCode);
}
