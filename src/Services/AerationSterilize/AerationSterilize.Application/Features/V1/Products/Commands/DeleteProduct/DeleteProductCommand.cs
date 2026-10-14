using AerationSterilize.Application.Features.V1.Products.Common.Caching;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Products.Commands.DeleteProduct;
public sealed record DeleteProductCommand(Guid Id) : ICommand, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [ProductCacheKeys.Prefix];
}
