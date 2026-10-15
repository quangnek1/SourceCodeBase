using Asp.Versioning;
using CommonServices.API.Abstractions;
using Infrastructure.Authorization;
using CommonServices.Application.Features.V1.Regions.Commands.CreateRegion;
using CommonServices.Application.Features.V1.Regions.Commands.DeleteRegion;
using CommonServices.Application.Features.V1.Regions.Commands.SetRegionStatus;
using CommonServices.Application.Features.V1.Regions.Commands.UpdateRegion;
using CommonServices.Application.Features.V1.Regions.Common.Dtos;
using CommonServices.Application.Features.V1.Regions.Queries.GetRegionById;
using CommonServices.Application.Features.V1.Regions.Queries.GetRegionList;
using CommonServices.Application.Features.V1.Regions.Queries.GetRegions;
using CommonServices.Domain.Enums;
using Contracts.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Common.Constants.Authorization;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.API.Controllers.V1;

[ApiVersion(1)]
public class RegionsController : ApiController
{
    public RegionsController(ISender sender) : base(sender)
    {
    }

    [HttpGet(Name = "GetRegions")]
    [ProducesResponseType(type: typeof(Result<PagedResult<RegionDto>>), statusCode: StatusCodes.Status200OK)]
    [Permission(FunctionCode.REGION, CommandCode.VIEW)]
    public async Task<IActionResult> GetRegions(string? searchTerm = null,
        ObjectStatus? status = null,
        string? sortColumn = null,
        string? sortOrder = null,
        int pageIndex = PagedResult<RegionDto>.DefaultPageIndex,
        int pageSize = PagedResult<RegionDto>.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetRegionsQuery(searchTerm, status, RequestLanguage, sortColumn,
            SortOrderExtension.ConvertStringToSortOrder(sortOrder),
            pageIndex,
            pageSize), cancellationToken);

        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("list")]
    [ProducesResponseType(type: typeof(Result<IReadOnlyList<RegionDto>>), statusCode: StatusCodes.Status200OK)]
    [Permission(FunctionCode.REGION, CommandCode.VIEW)]
    public async Task<IActionResult> GetRegionList(ObjectStatus? status = null, CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetRegionListQuery(RequestLanguage, status), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("{regionId:guid}")]
    [ProducesResponseType(type: typeof(Result<RegionDetailDto>), statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.REGION, CommandCode.VIEW)]
    public async Task<IActionResult> GetRegionById(Guid regionId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetRegionByIdQuery(regionId), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPost(Name = "CreateRegion")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Permission(FunctionCode.REGION, CommandCode.CREATE)]
    public async Task<IActionResult> CreateRegion([FromBody] CreateRegionCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{regionId:guid}")]
    [ProducesResponseType(typeof(Result<RegionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.REGION, CommandCode.UPDATE)]
    public async Task<IActionResult> UpdateRegion(Guid regionId, [FromBody] UpdateRegionCommand command, CancellationToken cancellationToken)
    {
        command.SetId(regionId);
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{regionId:guid}/activate")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.REGION, CommandCode.ACTIVATE)]
    public async Task<IActionResult> ActivateRegion(Guid regionId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new SetRegionStatusCommand(regionId, ObjectStatus.Active), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{regionId:guid}/deactivate")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.REGION, CommandCode.INACTIVATE)]
    public async Task<IActionResult> DeactivateRegion(Guid regionId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new SetRegionStatusCommand(regionId, ObjectStatus.Inactive), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpDelete("{regionId:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.REGION, CommandCode.DELETE)]
    public async Task<IActionResult> DeleteRegion(Guid regionId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteRegionCommand(regionId), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }
}
