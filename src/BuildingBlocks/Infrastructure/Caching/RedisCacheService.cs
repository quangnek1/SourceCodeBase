using System.Text.Json;
using Contracts.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Options;
using StackExchange.Redis;

namespace Infrastructure.Caching;

// Redis failures are logged and treated as a cache miss so the API keeps working without Redis.
internal sealed class RedisCacheService : ICacheService
{
    private const int DeleteBatchSize = 500;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _connection;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer connection, IOptions<RedisOptions> options, ILogger<RedisCacheService> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private IDatabase Database => _connection.GetDatabase();

    // While disconnected, commands would wait for the full timeout (5s by default) on every request, so skip Redis instead.
    private bool IsAvailable => _connection.IsConnected;

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return default;

        try
        {
            var value = await Database.StringGetAsync(BuildKey(key));
            return value.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>((string)value!, SerializerOptions);
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning(ex, "Redis GET failed for key {CacheKey}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            _logger.LogWarning("Redis is unavailable, skipped SET for key {CacheKey}", key);
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(value, SerializerOptions);
            await Database.StringSetAsync(BuildKey(key), json, expiration ?? TimeSpan.FromMinutes(_options.DefaultExpirationMinutes));
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning(ex, "Redis SET failed for key {CacheKey}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return false;

        try
        {
            return await Database.KeyExistsAsync(BuildKey(key));
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning(ex, "Redis EXISTS failed for key {CacheKey}", key);
            return false;
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            _logger.LogWarning("Redis is unavailable, skipped DEL for key {CacheKey}", key);
            return;
        }

        try
        {
            await Database.KeyDeleteAsync(BuildKey(key));
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning(ex, "Redis DEL failed for key {CacheKey}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            _logger.LogWarning("Redis is unavailable, skipped remove by prefix {CachePrefix}", prefix);
            return;
        }

        try
        {
            var database = Database;
            var pattern = BuildKey(prefix) + "*";

            foreach (var endpoint in _connection.GetEndPoints())
            {
                var server = _connection.GetServer(endpoint);
                if (!server.IsConnected || server.IsReplica)
                    continue;

                // KeysAsync uses SCAN, so it does not block the server like KEYS does.
                var batch = new List<RedisKey>(DeleteBatchSize);
                await foreach (var key in server.KeysAsync(database.Database, pattern, DeleteBatchSize).WithCancellation(cancellationToken))
                {
                    batch.Add(key);
                    if (batch.Count == DeleteBatchSize)
                    {
                        await database.KeyDeleteAsync(batch.ToArray());
                        batch.Clear();
                    }
                }

                if (batch.Count > 0)
                    await database.KeyDeleteAsync(batch.ToArray());
            }
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning(ex, "Redis remove by prefix failed for {CachePrefix}", prefix);
        }
    }

    private string BuildKey(string key) => _options.InstanceName + key;

    private static bool IsRedisFailure(Exception ex) => ex is RedisException or RedisTimeoutException;
}
