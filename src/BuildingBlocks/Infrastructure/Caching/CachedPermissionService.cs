using Contracts.Caching;
using Contracts.Identity;
using Shared.DTOs.Identity;

namespace Infrastructure.Caching;

// Decorates the database-backed IPermissionService. The entry is removed on logout.
internal sealed class CachedPermissionService : IPermissionService
{
    private static readonly TimeSpan Expiration = TimeSpan.FromMinutes(30);

    private readonly IPermissionService _inner;
    private readonly ICacheService _cacheService;

    public CachedPermissionService(IPermissionService inner, ICacheService cacheService)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    public async Task<IReadOnlyList<UserPermission>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var key = CacheKeys.Permissions(userId);

        var cached = await _cacheService.GetAsync<List<UserPermission>>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var permissions = await _inner.GetPermissionsAsync(userId, cancellationToken);
        await _cacheService.SetAsync(key, permissions, Expiration, cancellationToken);

        return permissions;
    }
}
