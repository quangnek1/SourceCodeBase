using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Roles.Commands.UpdateRole;
public sealed class UpdateRoleCommand : ICommand
{
    public Guid Id { get; private set; }
    public string Name { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public string? Description { get; set; }

    public void SetId(Guid id)
    {
        Id = id;
    }
}
