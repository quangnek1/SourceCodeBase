using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using AerationSterilize.Application.Features.V1.Products.Common.Extensions;
using AerationSterilize.Domain.Entities;
using AutoMapper;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Shared.Emumerations;
using Shared.Paging;

namespace AerationSterilize.Application.Features.V1.Products.Queries.GetProducts;
public class GetProductsQueryHandler : IQueryHandler<GetProductsQuery, PagedResult<ProductDto>>
{
    private readonly IRepositoryBase<Product, Guid> _productRepository;
    private readonly IMapper _mapper;

    public GetProductsQueryHandler(IRepositoryBase<Product, Guid> productRepository, IMapper mapper)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Result<PagedResult<ProductDto>>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var productsQuery = string.IsNullOrWhiteSpace(request.searchTerm)
          ? _productRepository.FindAll()
          : _productRepository.FindAll(x => x.Name.Contains(request.searchTerm) || x.Description.Contains(request.searchTerm));

        var sortExpression = ProductExtension.GetSortExpression(request.sortColumn);

        productsQuery = request.SortOrder == SortOrder.Descending
          ? productsQuery.OrderByDescending(sortExpression)
          : productsQuery.OrderBy(sortExpression);

        var pagedResult = await PagingExtensions.ToPagedResultAsync(
                        productsQuery,
                        request.PageIndex,
                        request.PageSize);

        var result = _mapper.Map<PagedResult<ProductDto>>(pagedResult);

        return Result.Success(result);
    }
}
