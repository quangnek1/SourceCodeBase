using AerationSterilize.Application.Common;
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
    private readonly ICurrentUser _currentUser;

    public CreateProductCommandHandler(IRepositoryBase<Product, Guid> productRepository, IMapper mapper, ICurrentUser currentUser)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }
    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var productEntity = _mapper.Map<Product>(request);
        productEntity.CreatedBy = _currentUser.UserId;
        productEntity.DepartmentId = _currentUser.DepartmentId;

        _productRepository.Add(productEntity);

        return Result.Success(productEntity.Id);
    }
}

