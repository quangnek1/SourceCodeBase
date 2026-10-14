using AerationSterilize.Application.Features.V1.Permissions.Common.Dtos;
using AerationSterilize.Domain.Entities.Identity;
using AerationSterilize.Domain.Repositories;
using Contracts.Common.Messages;
using Contracts.Responses;

namespace AerationSterilize.Application.Features.V1.Permissions.Queries.GetPermissionMatrix;
internal sealed class GetPermissionMatrixQueryHandler : IQueryHandler<GetPermissionMatrixQuery, PermissionMatrixDto>
{
    private readonly IPermissionRepository _permissionRepository;

    public GetPermissionMatrixQueryHandler(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository ?? throw new ArgumentNullException(nameof(permissionRepository));
    }

    public async Task<Result<PermissionMatrixDto>> Handle(GetPermissionMatrixQuery request, CancellationToken cancellationToken)
    {
        var functions = await _permissionRepository.GetActiveFunctionsAsync(cancellationToken);
        var actions = await _permissionRepository.GetActiveActionsAsync(cancellationToken);
        var actionInFunctions = await _permissionRepository.GetActionInFunctionsAsync(cancellationToken);

        // Keep the column order of the actions for each row.
        var actionOrder = actions.Select((a, index) => (a.Id, index)).ToDictionary(x => x.Id, x => x.index);
        var allowedByFunction = actionInFunctions
            .Where(aif => actionOrder.ContainsKey(aif.ActionId))
            .GroupBy(aif => aif.FunctionId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(aif => aif.ActionId).OrderBy(id => actionOrder[id]).ToList());

        var functionIds = functions.Select(f => f.Id).ToHashSet();
        var childrenByParent = functions
            .Where(f => IsChild(f, functionIds))
            .GroupBy(f => f.ParrentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        FunctionNodeDto ToNode(Function function) => new(
            function.Id,
            function.Name,
            IsChild(function, functionIds) ? function.ParrentId : null,
            function.SortOrder,
            allowedByFunction.GetValueOrDefault(function.Id, []),
            childrenByParent.TryGetValue(function.Id, out var children)
                ? children.OrderBy(c => c.SortOrder).Select(ToNode).ToList()
                : []);

        var roots = functions
            .Where(f => !IsChild(f, functionIds))
            .OrderBy(f => f.SortOrder)
            .Select(ToNode)
            .ToList();

        var matrix = new PermissionMatrixDto(
            actions.Select(a => new ActionDto(a.Id, a.Name)).ToList(),
            roots);

        return Result.Success(matrix);
    }

    // A function whose parent is missing or inactive is shown as a root, so it never disappears from the matrix.
    private static bool IsChild(Function function, HashSet<string> functionIds)
        => !string.IsNullOrEmpty(function.ParrentId) && functionIds.Contains(function.ParrentId);
}
