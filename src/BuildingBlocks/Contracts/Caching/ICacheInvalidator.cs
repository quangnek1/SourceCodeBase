namespace Contracts.Caching;

// Commands implementing this clear every cache key starting with one of the prefixes after they succeed.
public interface ICacheInvalidator
{
    IEnumerable<string> CacheKeyPrefixes { get; }
}
