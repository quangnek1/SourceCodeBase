using Shared.Common.Constants.Authorization;

namespace AerationSterilize.Application.Features.V1.Permissions.Common.Dtos;

// Matrix layout for the permission screen: Functions are the rows (as a tree), Actions are the columns.
// AllowedActions says which cells of a row can be ticked.
public sealed record PermissionMatrixDto(
    IReadOnlyList<ActionDto> Actions,
    IReadOnlyList<FunctionNodeDto> Functions);

public sealed record ActionDto(string Id, string Name);

public sealed record FunctionNodeDto(
    string Id,
    string Name,
    string? ParentId,
    int? SortOrder,
    IReadOnlyList<string> AllowedActions,
    IReadOnlyList<FunctionNodeDto> Children);

// One ticked cell of the matrix.
public sealed record RolePermissionDto(
    string FunctionId,
    string ActionId,
    AccessLevel AccessLevel);
