using Shared.Common.Constants.Authorization;

namespace AerationSterilize.Application.Common;
public interface ICurrentUser
{
    Guid UserId { get; }
    Guid? DepartmentId { get; }
    AccessLevel GetAccessLevel(string functionCode, string commandCode);
}
