using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Roles.Commands.CreateRole;
public sealed record CreateRoleCommand(
    string Name,
    string RoleCode,
    string? Description) : ICommand<Guid>;
