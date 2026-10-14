using AerationSterilize.Application.Features.V1.Permissions.Common.Dtos;
using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Permissions.Queries.GetPermissionMatrix;
public sealed record GetPermissionMatrixQuery : IQuery<PermissionMatrixDto>;
