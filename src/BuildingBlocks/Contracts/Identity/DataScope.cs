using Contracts.Domains.Interfaces;
using Shared.Common.Constants.Authorization;

namespace Contracts.Identity;

// The rows of an IDataOwned entity the current user may access for one permission (FunctionCode + CommandCode).
public sealed record DataScope(AccessLevel Level, Guid UserId, Guid? DepartmentId)
{
    // Added to the cache key of scoped queries, so users with a different scope never share a cached result.
    public string CacheKey => Level switch
    {
        AccessLevel.All => "all",
        AccessLevel.Department => $"dept:{DepartmentId}",
        AccessLevel.Own => $"own:{UserId}",
        _ => "none"
    };

    public bool Covers(IDataOwned entity) => Level switch
    {
        AccessLevel.All => true,
        AccessLevel.Department => DepartmentId is not null && entity.DepartmentId == DepartmentId,
        AccessLevel.Own => entity.CreatedBy == UserId,
        _ => false
    };
}

public static class DataScopeExtensions
{
    public static IQueryable<T> ApplyDataScope<T>(this IQueryable<T> query, DataScope scope)
        where T : IDataOwned
    {
        var userId = scope.UserId;
        var departmentId = scope.DepartmentId;

        return scope.Level switch
        {
            AccessLevel.All => query,
            // A user without a department sees nothing, instead of every row whose DepartmentId is null.
            AccessLevel.Department when departmentId is not null => query.Where(x => x.DepartmentId == departmentId),
            AccessLevel.Own => query.Where(x => x.CreatedBy == userId),
            _ => query.Where(x => false)
        };
    }
}
