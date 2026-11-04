using AerationSterilize.Application.Features.V1.Products.Common.Caching;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Products.Commands.CreateProduct;
/// <summary>
/// Command tao moi Product (task sp-1801 - fix bug QC)
/// </summary>
public sealed record CreateProductCommand(
    string Name,
    decimal Price,
    string Description
) : ICommand<Guid>, ICacheInvalidator
{
    /// <summary>
    /// Bo sung cached
    /// </summary>
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [ProductCacheKeys.Prefix];
}
