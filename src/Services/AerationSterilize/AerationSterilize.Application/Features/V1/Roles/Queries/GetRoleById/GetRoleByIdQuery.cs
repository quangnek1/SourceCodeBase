using AerationSterilize.Application.Features.V1.Roles.Common.Dtos;
using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Roles.Queries.GetRoleById;
public sealed record GetRoleByIdQuery(Guid Id) : IQuery<RoleDto>;
