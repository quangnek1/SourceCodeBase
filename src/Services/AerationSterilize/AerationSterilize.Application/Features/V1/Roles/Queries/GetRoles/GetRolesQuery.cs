using AerationSterilize.Application.Features.V1.Roles.Common.Dtos;
using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Roles.Queries.GetRoles;
public sealed record GetRolesQuery : IQuery<IReadOnlyList<RoleDto>>;
