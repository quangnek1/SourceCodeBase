using CommonServices.Domain.Enums;

namespace CommonServices.Application.Features.V1.Countries.Common.Caching;
public static class CountryCacheKeys
{
    public const string Prefix = "country:";

    public static string ById(Guid id) => $"{Prefix}{id}";

    public static string List(string langCode, ObjectStatus? status, IReadOnlyCollection<Guid>? regionIds)
        => $"{Prefix}list:{langCode.ToLowerInvariant()}:{status?.ToString() ?? "all"}:{JoinIds(regionIds)}";

    public static string Page(string? searchTerm, ObjectStatus? status, Guid? regionId, string langCode, string? sortColumn, string? sortOrder, int pageIndex, int pageSize)
        => $"{Prefix}page:{searchTerm?.Trim().ToLowerInvariant()}:{status}:{regionId}:{langCode.ToLowerInvariant()}:{sortColumn?.ToLowerInvariant()}:{sortOrder}:{pageIndex}:{pageSize}";

    private static string JoinIds(IReadOnlyCollection<Guid>? ids)
        => ids is null || ids.Count == 0 ? "all" : string.Join(",", ids.Order());
}
