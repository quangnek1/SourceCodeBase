using AerationSterilize.Application.Common;
using Contracts.Domains.Interfaces;
using Shared.Common.Constants.Authorization;

namespace AerationSterilize.API.DependencyInjection.Extensions;

public static class DataScopeExtensions
{
    public static IQueryable<T> ApplyDataScope<T>(
    this IQueryable<T> query, ICurrentUser currentUser, string functionCode, string commandCode)
    where T : IDataOwned
    {
        return currentUser.GetAccessLevel(functionCode, commandCode) switch
        {
            AccessLevel.All => query,
            AccessLevel.Department => query.Where(x => x.DepartmentId == currentUser.DepartmentId),
            AccessLevel.Own => query.Where(x => x.CreatedBy == currentUser.UserId),
            _ => query.Where(x => false)
        };
    }
}
