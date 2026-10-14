using CommonServices.Domain.Enums;

namespace CommonServices.Application.Features.V1.Cities.Common.Caching;
public static class CityCacheKeys
{
    public const string Prefix = "city:";

    public static string ById(Guid id) => $"{Prefix}{id}";

    public static string List(string langCode, ObjectStatus? status, IReadOnlyCollection<Guid>? countryIds)
        => $"{Prefix}list:{langCode.ToLowerInvariant()}:{status?.ToString() ?? "all"}:{JoinIds(countryIds)}";

    public static string Page(string? searchTerm, ObjectStatus? status, Guid? countryId, string langCode, string? sortColumn, string? sortOrder, int pageIndex, int pageSize)
        => $"{Prefix}page:{searchTerm?.Trim().ToLowerInvariant()}:{status}:{countryId}:{langCode.ToLowerInvariant()}:{sortColumn?.ToLowerInvariant()}:{sortOrder}:{pageIndex}:{pageSize}";

    private static string JoinIds(IReadOnlyCollection<Guid>? ids)
        => ids is null || ids.Count == 0 ? "all" : string.Join(",", ids.Order());
}
