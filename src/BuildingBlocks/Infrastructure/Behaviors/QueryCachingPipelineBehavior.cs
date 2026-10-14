using System.Reflection;
using Contracts.Caching;
using Contracts.Responses;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Behaviors;

// Caches the Value of a successful Result<T> for queries implementing ICachedQuery.
// Result<T> cannot be deserialized directly, so only the value is stored and the Result is rebuilt on a hit.
public sealed class QueryCachingPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICachedQuery
    where TResponse : Result
{
    private static readonly MethodInfo HandleCachedMethod =
        typeof(QueryCachingPipelineBehavior<TRequest, TResponse>)
            .GetMethod(nameof(HandleCachedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ICacheService _cacheService;
    private readonly ILogger<QueryCachingPipelineBehavior<TRequest, TResponse>> _logger;

    public QueryCachingPipelineBehavior(ICacheService cacheService, ILogger<QueryCachingPipelineBehavior<TRequest, TResponse>> logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var responseType = typeof(TResponse);
        if (!responseType.IsGenericType || responseType.GetGenericTypeDefinition() != typeof(Result<>))
            return next(cancellationToken);

        var valueType = responseType.GetGenericArguments()[0];
        return (Task<TResponse>)HandleCachedMethod
            .MakeGenericMethod(valueType)
            .Invoke(this, [request, next, cancellationToken])!;
    }

    private async Task<TResponse> HandleCachedAsync<TValue>(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var cached = await _cacheService.GetAsync<TValue>(request.CacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for {CacheKey}", request.CacheKey);
            return (TResponse)(object)Result.Success(cached);
        }

        _logger.LogDebug("Cache miss for {CacheKey}", request.CacheKey);
        var response = await next(cancellationToken);

        // A null value (e.g. "not found") is not cached, otherwise it would hide a record created later.
        if (response is Result<TValue> { IsSuccess: true, Value: not null } result)
            await _cacheService.SetAsync(request.CacheKey, result.Value, request.Expiration, cancellationToken);

        return response;
    }
}
