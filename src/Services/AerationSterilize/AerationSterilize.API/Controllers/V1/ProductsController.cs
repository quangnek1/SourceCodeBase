using AerationSterilize.API.Abstractions;
using AerationSterilize.Application.Common;
using Infrastructure.Authorization;
using AerationSterilize.Application.Features.V1.Products.Commands.CreateProduct;
using AerationSterilize.Application.Features.V1.Products.Commands.DeleteProduct;
using AerationSterilize.Application.Features.V1.Products.Commands.UpdateProduct;
using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using AerationSterilize.Application.Features.V1.Products.Queries.GetProductById;
using AerationSterilize.Application.Features.V1.Products.Queries.GetProducts;
using Asp.Versioning;
using Contracts.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Common.Constants.Authorization;
using Shared.Emumerations;

namespace AerationSterilize.API.Controllers.V1;

[ApiVersion(1)]
public class ProductsController : ApiController
{
    private readonly ICurrentUser _currentUser;

    public ProductsController(ISender sender, ICurrentUser currentUser) : base(sender)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    [HttpGet(Name = "GetProducts")]
    [ProducesResponseType(type: typeof(Result<IEnumerable<ProductDto>>), statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.PRODUCT, CommandCode.VIEW)]
    public async Task<IActionResult> GetProducts(string? searchTerm = null,
       string? sortColumn = null,
       string? sortOrder = null,
       int pageIndex = 1,
       int pageSize = 10)
    {
        var result = await Sender.Send(new GetProductsQuery(searchTerm, sortColumn,
            SortOrderExtension.ConvertStringToSortOrder(sortOrder),
            pageIndex,
            pageSize,
            _currentUser.GetDataScope(FunctionCode.PRODUCT, CommandCode.VIEW)));

        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("{productId}")]
    [ProducesResponseType(type: typeof(Result<ProductDto>), statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.PRODUCT, CommandCode.VIEW)]
    public async Task<IActionResult> GetProductById(Guid productId)
    {
        var result = await Sender.Send(new GetProductByIdQuery(productId, _currentUser.GetDataScope(FunctionCode.PRODUCT, CommandCode.VIEW)));
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPost(Name = "CreateProducts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.PRODUCT, CommandCode.CREATE)]
    public async Task<IActionResult> CreateProducts([FromBody] CreateProductCommand command)
    {
        var result = await Sender.Send(command);

        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpDelete("{productId}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.PRODUCT, CommandCode.DELETE)]
    public async Task<IActionResult> DeleteProduct(Guid productId)
    {
        var result = await Sender.Send(new DeleteProductCommand(productId));
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{productId}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.PRODUCT, CommandCode.UPDATE)]
    public async Task<IActionResult> UpdateProductById(Guid productId, [FromBody] UpdateProductCommand command)
    {
        command.SetId(productId);
        var result = await Sender.Send(command);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }
}
