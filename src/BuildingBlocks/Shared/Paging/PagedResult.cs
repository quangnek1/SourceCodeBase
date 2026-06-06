namespace Shared.Paging;

public sealed class PagedResult<T>
{
    public const int UpperPageSize = 100;
    public const int DefaultPageSize = 10;
    public const int DefaultPageIndex = 1;
    private PagedResult(List<T> items, int pageIndex, int pageSize, int totalCount)
    {
        Items = items;
        PageIndex = pageIndex;
        PageSize = pageSize;
        TotalCount = totalCount;
    }
    public PagedResult()
    {
        
    }
    public IReadOnlyList<T> Items { get; init; } = [];
    public int PageIndex { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => PageIndex < TotalPages;
    public bool HasPreviousPage => PageIndex > 1;


}
