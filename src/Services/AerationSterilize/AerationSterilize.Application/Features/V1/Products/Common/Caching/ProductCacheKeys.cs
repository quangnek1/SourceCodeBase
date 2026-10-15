namespace AerationSterilize.Application.Features.V1.Products.Common.Caching;
public static class ProductCacheKeys
{
    public const string Prefix = "product:";

    // scope = DataScope.CacheKey: the same product/page is cached separately per data scope.
    public static string ById(Guid id, string scope) => $"{Prefix}{scope}:{id}";

    public static string List(string scope, string? searchTerm, string? sortColumn, string? sortOrder, int pageIndex, int pageSize)
        => $"{Prefix}{scope}:list:{searchTerm?.Trim().ToLowerInvariant()}:{sortColumn?.ToLowerInvariant()}:{sortOrder}:{pageIndex}:{pageSize}";
}
