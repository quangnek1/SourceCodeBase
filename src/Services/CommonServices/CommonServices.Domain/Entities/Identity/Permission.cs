using Shared.Common.Constants.Authorization;

namespace CommonServices.Domain.Entities.Identity;
public class Permission
{
    public Guid RoleId { get; set; }
    public string FunctionId { get; set; }
    public string ActionId { get; set; }
    /// <summary>
    /// Phân quyền theo dữ liệu
    /// </summary>
    public AccessLevel AccessLevel { get; set; } = AccessLevel.All;
}
