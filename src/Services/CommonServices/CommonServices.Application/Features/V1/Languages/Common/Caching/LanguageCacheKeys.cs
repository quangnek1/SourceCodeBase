using CommonServices.Domain.Enums;

namespace CommonServices.Application.Features.V1.Languages.Common.Caching;
public static class LanguageCacheKeys
{
    public const string Prefix = "language:";

    public const string Native = $"{Prefix}native";

    public static string ById(Guid id) => $"{Prefix}{id}";

    public static string List(string langCode, ObjectStatus? status)
        => $"{Prefix}list:{langCode.ToLowerInvariant()}:{status?.ToString() ?? "all"}";

    public static string Page(string? searchTerm, ObjectStatus? status, string langCode, string? sortColumn, string? sortOrder, int pageIndex, int pageSize)
        => $"{Prefix}page:{searchTerm?.Trim().ToLowerInvariant()}:{status}:{langCode.ToLowerInvariant()}:{sortColumn?.ToLowerInvariant()}:{sortOrder}:{pageIndex}:{pageSize}";
}
