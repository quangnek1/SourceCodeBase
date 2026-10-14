using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using Contracts.Common.Messages;
using Shared.Emumerations;
using Shared.Paging;

namespace AerationSterilize.Application.Features.V1.Products.Queries.GetProducts;
public record GetProductsQuery(
    string? searchTerm,
    string? sortColumn,
    SortOrder? SortOrder,
    int PageIndex, int PageSize)
     : IQuery<PagedResult<ProductDto>>;
