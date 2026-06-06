using Microsoft.EntityFrameworkCore;
namespace Shared.Paging;
public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int pageIndex,
        int pageSize)
    {
        var totalCount = await query.CountAsync();

        var items = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

        return new PagedResult<T>
        {
            Items = items,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
