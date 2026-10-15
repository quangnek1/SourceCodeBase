using Contracts.Caching;
using Contracts.Identity;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Infrastructure.Identity;

// Deny-list of access token ids (jti). Each entry lives only until the token would have expired anyway.
// Keys are written without RedisOptions.InstanceName: a token revoked by AerationSterilize (logout)
// must also be rejected by every other service that accepts it.
internal sealed class RedisTokenRevocationService : ITokenRevocationService
{
    private readonly IConnectionMultiplexer _connection;
    private readonly ILogger<RedisTokenRevocationService> _logger;

    public RedisTokenRevocationService(IConnectionMultiplexer connection, ILogger<RedisTokenRevocationService> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RevokeAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        var remaining = expiresAt - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return;

        if (!_connection.IsConnected)
        {
            _logger.LogWarning("Redis is unavailable, could not revoke token {Jti}", jti);
            return;
        }

        try
        {
            await _connection.GetDatabase().StringSetAsync(CacheKeys.RevokedToken(jti), true, remaining);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Redis SET failed while revoking token {Jti}", jti);
        }
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (!_connection.IsConnected)
            return false;

        try
        {
            return await _connection.GetDatabase().KeyExistsAsync(CacheKeys.RevokedToken(jti));
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Redis EXISTS failed for revoked token {Jti}", jti);
            return false;
        }
    }
}
