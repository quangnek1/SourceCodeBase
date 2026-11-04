using AerationSterilize.Domain.Entities;
using AutoMapper;
using static AerationSterilize.Domain.Exceptions.ProductException;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;

namespace AerationSterilize.Application.Features.V1.Products.Commands.DeleteProduct;
internal sealed class DeleteProductCommandHandler : ICommandHandler<DeleteProductCommand>
{
    private readonly IRepositoryBase<Product, Guid> _productRepository;

    /// <summary>
    /// Khoi tao DeleteProductCommandHandler (task sp-1942 commit 2)
    /// </summary>
    public DeleteProductCommandHandler(IRepositoryBase<Product, Guid> productRepository)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    }

    /// <summary>
    /// Handler xu ly xoa Product (task sp-1942)
    /// </summary>
    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var productEntity = await _productRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new ProductNotFoundException(request.Id);

        _productRepository.Remove(productEntity);

        return Result.Success();
    }
}

