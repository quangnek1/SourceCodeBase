using CommonServices.Domain.Enums;

namespace CommonServices.Application.Features.V1.Currencies.Common.Caching;
public static class CurrencyCacheKeys
{
    public const string Prefix = "currency:";

    public static string ById(Guid id) => $"{Prefix}{id}";

    public static string List(string langCode, ObjectStatus? status)
        => $"{Prefix}list:{langCode.ToLowerInvariant()}:{status?.ToString() ?? "all"}";

    public static string Page(string? searchTerm, ObjectStatus? status, string langCode, string? sortColumn, string? sortOrder, int pageIndex, int pageSize)
        => $"{Prefix}page:{searchTerm?.Trim().ToLowerInvariant()}:{status}:{langCode.ToLowerInvariant()}:{sortColumn?.ToLowerInvariant()}:{sortOrder}:{pageIndex}:{pageSize}";
}
