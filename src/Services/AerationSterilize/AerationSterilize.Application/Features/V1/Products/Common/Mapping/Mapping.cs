using AerationSterilize.Application.Features.V1.Products.Commands.CreateProduct;
using AerationSterilize.Application.Features.V1.Products.Commands.UpdateProduct;
using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using AerationSterilize.Domain.Entities;
using AutoMapper;
using Shared.Paging;

namespace AerationSterilize.Application.Features.V1.Products.Common.Mapping;
public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<CreateProductCommand, Product>();
        CreateMap<UpdateProductCommand, Product>();
        CreateMap<Product, ProductDto>();
        CreateMap<PagedResult<Product>, PagedResult<ProductDto>>().ReverseMap();
    }
}
