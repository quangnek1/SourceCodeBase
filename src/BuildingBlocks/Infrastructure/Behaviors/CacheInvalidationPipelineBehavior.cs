using Contracts.Caching;
using Contracts.Responses;
using MediatR;

namespace Infrastructure.Behaviors;

// Registered outside TransactionPipelineBehavior so the cache is cleared only after the transaction has committed.
public sealed class CacheInvalidationPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICacheInvalidator
    where TResponse : Result
{
    private readonly ICacheService _cacheService;

    public CacheInvalidationPipelineBehavior(ICacheService cacheService)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);

        if (response.IsSuccess)
        {
            foreach (var prefix in request.CacheKeyPrefixes)
                await _cacheService.RemoveByPrefixAsync(prefix, cancellationToken);
        }

        return response;
    }
}
