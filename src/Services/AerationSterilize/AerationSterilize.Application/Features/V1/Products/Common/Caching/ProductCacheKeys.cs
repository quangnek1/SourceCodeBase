namespace AerationSterilize.Application.Features.V1.Products.Common.Caching;
public static class ProductCacheKeys
{
    public const string Prefix = "product:";

    public static string ById(Guid id) => $"{Prefix}{id}";

    public static string List(string? searchTerm, string? sortColumn, string? sortOrder, int pageIndex, int pageSize)
        => $"{Prefix}list:{searchTerm?.Trim().ToLowerInvariant()}:{sortColumn?.ToLowerInvariant()}:{sortOrder}:{pageIndex}:{pageSize}";
}
