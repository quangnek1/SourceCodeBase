using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using AerationSterilize.Domain.Entities;
using AutoMapper;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;

namespace AerationSterilize.Application.Features.V1.Products.Queries.GetProductById;
public class GetProductByIdQueryHandler : IQueryHandler<GetProductByIdQuery, ProductDto>
{
    private readonly IRepositoryBase<Product, Guid> _productRepository;
    private readonly IMapper _mapper;

    public GetProductByIdQueryHandler(IRepositoryBase<Product, Guid> productRepository, IMapper mapper)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.FindByIdAsync(request.Id);

        var result = _mapper.Map<ProductDto>(product);

        return Result.Success(result);
    }
}
