using Contracts.Common.Messages;

namespace Contracts.Caching;
public interface ICachedQuery
{
    string CacheKey { get; }

    // null => RedisOptions.DefaultExpirationMinutes
    TimeSpan? Expiration { get; }
}

public interface ICachedQuery<TResponse> : IQuery<TResponse>, ICachedQuery;
