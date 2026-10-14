using AerationSterilize.Domain.Entities;
using AutoMapper;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;

namespace AerationSterilize.Application.Features.V1.Products.Commands.DeleteProduct;
internal sealed class DeleteProductCommandHandler : ICommandHandler<DeleteProductCommand>
{
    private readonly IRepositoryBase<Product, Guid> _productRepository;

    public DeleteProductCommandHandler(IRepositoryBase<Product, Guid> productRepository)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    }

    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var productEntity = await _productRepository.FindByIdAsync(request.Id);

        _productRepository.Remove(productEntity);

        return Result.Success();
    }
}

