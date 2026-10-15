using AerationSterilize.Application.Common;
using AerationSterilize.Domain.Entities;
using AutoMapper;
using static AerationSterilize.Domain.Exceptions.ProductException;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Shared.Common.Constants.Authorization;

namespace AerationSterilize.Application.Features.V1.Products.Commands.DeleteProduct;
internal sealed class DeleteProductCommandHandler : ICommandHandler<DeleteProductCommand>
{
    private readonly IRepositoryBase<Product, Guid> _productRepository;
    private readonly ICurrentUser _currentUser;

    public DeleteProductCommandHandler(IRepositoryBase<Product, Guid> productRepository, ICurrentUser currentUser)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var productEntity = await _productRepository.FindByIdAsync(request.Id, cancellationToken);

        // Outside the user's data scope is reported as not found, so the product's existence is not revealed.
        var scope = _currentUser.GetDataScope(FunctionCode.PRODUCT, CommandCode.DELETE);
        if (productEntity is null || !scope.Covers(productEntity))
            throw new ProductNotFoundException(request.Id);

        _productRepository.Remove(productEntity);

        return Result.Success();
    }
}

