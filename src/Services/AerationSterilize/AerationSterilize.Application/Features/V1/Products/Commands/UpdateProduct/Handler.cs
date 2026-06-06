using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using AerationSterilize.Domain.Entities;
using AutoMapper;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;

namespace AerationSterilize.Application.Features.V1.Products.Commands.UpdateProduct;

internal sealed class UpdateProductCommandHandler : ICommandHandler<UpdateProductCommand, ProductDto>
{
    private readonly IRepositoryBase<Product, Guid> _productRepository;
    private readonly IMapper _mapper;

    public UpdateProductCommandHandler(IRepositoryBase<Product, Guid> productRepository, IMapper mapper)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }
    public async Task<Result<ProductDto>> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var productEntity = await _productRepository.FindByIdAsync(request.Id);

        productEntity = _mapper.Map(request, productEntity);

        _productRepository.Update(productEntity);

        var result = _mapper.Map<ProductDto>(productEntity);

        return Result.Success(result);
    }
}
