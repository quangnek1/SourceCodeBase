using AerationSterilize.Application.Features.V1.Permissions.Common.Dtos;
using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Permissions.Queries.GetRolePermissions;
public sealed record GetRolePermissionsQuery(Guid RoleId) : IQuery<IReadOnlyList<RolePermissionDto>>;
