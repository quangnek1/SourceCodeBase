using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Roles.Commands.DeleteRole;
public sealed record DeleteRoleCommand(Guid Id) : ICommand;
