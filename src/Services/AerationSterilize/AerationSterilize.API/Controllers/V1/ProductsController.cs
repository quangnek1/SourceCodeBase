using AerationSterilize.API.Abstractions;
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
using Shared.Emumerations;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace AerationSterilize.API.Controllers.V1;

[ApiVersion(1)]
public class ProductsController : ApiController
{
    public ProductsController(ISender sender) : base(sender)
    {
    }

    [HttpGet(Name = "GetProducts")]
    [ProducesResponseType(type: typeof(Result<IEnumerable<ProductDto>>), statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProducts(string? searchTerm = null,
       string? sortColumn = null,
       string? sortOrder = null,
       int pageIndex = 1,
       int pageSize = 10)
    {
        var result = await Sender.Send(new GetProductsQuery(searchTerm, sortColumn,
            SortOrderExtension.ConvertStringToSortOrder(sortOrder),
            pageIndex,
            pageSize));

        return Ok(result);
    }

    [HttpGet("{productId}")]
    [ProducesResponseType(type: typeof(Result<ProductDto>), statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductById(Guid productId)
    {
        var result = await Sender.Send(new GetProductByIdQuery(productId));
        return Ok(result);
    }

    [HttpPost(Name = "CreateProducts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateProducts([FromBody] CreateProductCommand command)
    {
        var result = await Sender.Send(command);

        return Ok(result);
    }

    [HttpDelete("{productId}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct(Guid productId)
    {
        var result = await Sender.Send(new DeleteProductCommand(productId));
        return Ok(result);
    }

    [HttpPut("{productId}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProductById(Guid productId, [FromBody] UpdateProductCommand command)
    {
        command.SetId(productId);
        var result = await Sender.Send(command);
        return Ok(result);
    }
}
