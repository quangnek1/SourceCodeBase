using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using AerationSterilize.Application.Common;
using AerationSterilize.Domain.Entities;
using static AerationSterilize.Domain.Exceptions.ProductException;
using AutoMapper;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Shared.Common.Constants.Authorization;

namespace AerationSterilize.Application.Features.V1.Products.Commands.UpdateProduct;

internal sealed class UpdateProductCommandHandler : ICommandHandler<UpdateProductCommand, ProductDto>
{
    private readonly IRepositoryBase<Product, Guid> _productRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public UpdateProductCommandHandler(IRepositoryBase<Product, Guid> productRepository, IMapper mapper, ICurrentUser currentUser)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }
    public async Task<Result<ProductDto>> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var productEntity = await _productRepository.FindByIdAsync(request.Id, cancellationToken);

        // Outside the user's data scope is reported as not found, so the product's existence is not revealed.
        var scope = _currentUser.GetDataScope(FunctionCode.PRODUCT, CommandCode.UPDATE);
        if (productEntity is null || !scope.Covers(productEntity))
            throw new ProductNotFoundException(request.Id);

        productEntity = _mapper.Map(request, productEntity);

        _productRepository.Update(productEntity);

        var result = _mapper.Map<ProductDto>(productEntity);

        return Result.Success(result);
    }
}
