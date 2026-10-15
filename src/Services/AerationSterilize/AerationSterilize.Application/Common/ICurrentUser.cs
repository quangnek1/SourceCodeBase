using Contracts.Identity;
using Shared.Common.Constants.Authorization;

namespace AerationSterilize.Application.Common;
public interface ICurrentUser
{
    Guid UserId { get; }
    Guid? DepartmentId { get; }
    string? TokenId { get; }
    DateTimeOffset? TokenExpiresAt { get; }
    AccessLevel GetAccessLevel(string functionCode, string commandCode);
    DataScope GetDataScope(string functionCode, string commandCode);
}
