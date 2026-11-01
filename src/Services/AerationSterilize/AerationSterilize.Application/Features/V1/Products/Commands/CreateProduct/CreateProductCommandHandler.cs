using AerationSterilize.Domain.Entities;
using AutoMapper;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;

namespace AerationSterilize.Application.Features.V1.Products.Commands.CreateProduct;
internal sealed class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Guid>
{
    private readonly IRepositoryBase<Product, Guid> _productRepository;
    private readonly IMapper _mapper;

    public CreateProductCommandHandler(IRepositoryBase<Product, Guid> productRepository, IMapper mapper)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }
    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var productEntity = _mapper.Map<Product>(request);

        _productRepository.Add(productEntity);

        return Result.Success(productEntity.Id);
    }
}

