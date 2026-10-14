using Contracts.Caching;
using Contracts.Identity;

namespace Infrastructure.Identity;

// Deny-list of access token ids (jti). Each entry lives only until the token would have expired anyway.
internal sealed class RedisTokenRevocationService : ITokenRevocationService
{
    private readonly ICacheService _cacheService;

    public RedisTokenRevocationService(ICacheService cacheService)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    public async Task RevokeAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        var remaining = expiresAt - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return;

        await _cacheService.SetAsync(CacheKeys.RevokedToken(jti), true, remaining, cancellationToken);
    }

    public Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default)
        => _cacheService.ExistsAsync(CacheKeys.RevokedToken(jti), cancellationToken);
}
