using Asp.Versioning;
using CommonServices.API.Abstractions;
using Infrastructure.Authorization;
using CommonServices.Application.Features.V1.Cities.Commands.CreateCity;
using CommonServices.Application.Features.V1.Cities.Commands.DeleteCity;
using CommonServices.Application.Features.V1.Cities.Commands.SetCityStatus;
using CommonServices.Application.Features.V1.Cities.Commands.UpdateCity;
using CommonServices.Application.Features.V1.Cities.Common.Dtos;
using CommonServices.Application.Features.V1.Cities.Queries.GetCities;
using CommonServices.Application.Features.V1.Cities.Queries.GetCityById;
using CommonServices.Application.Features.V1.Cities.Queries.GetCityList;
using CommonServices.Domain.Enums;
using Contracts.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Common.Constants.Authorization;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.API.Controllers.V1;

[ApiVersion(1)]
public class CitiesController : ApiController
{
    public CitiesController(ISender sender) : base(sender)
    {
    }

    [HttpGet(Name = "GetCities")]
    [ProducesResponseType(type: typeof(Result<PagedResult<CityDto>>), statusCode: StatusCodes.Status200OK)]
    [Permission(FunctionCode.CITY, CommandCode.VIEW)]
    public async Task<IActionResult> GetCities(string? searchTerm = null,
        ObjectStatus? status = null,
        Guid? countryId = null,
        string? sortColumn = null,
        string? sortOrder = null,
        int pageIndex = PagedResult<CityDto>.DefaultPageIndex,
        int pageSize = PagedResult<CityDto>.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetCitiesQuery(searchTerm, status, countryId, RequestLanguage, sortColumn,
            SortOrderExtension.ConvertStringToSortOrder(sortOrder),
            pageIndex,
            pageSize), cancellationToken);

        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("list")]
    [ProducesResponseType(type: typeof(Result<IReadOnlyList<CityDto>>), statusCode: StatusCodes.Status200OK)]
    [Permission(FunctionCode.CITY, CommandCode.VIEW)]
    public async Task<IActionResult> GetCityList(ObjectStatus? status = null,
        [FromQuery] Guid[]? countryIds = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetCityListQuery(RequestLanguage, status, countryIds), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("{cityId:guid}")]
    [ProducesResponseType(type: typeof(Result<CityDetailDto>), statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.CITY, CommandCode.VIEW)]
    public async Task<IActionResult> GetCityById(Guid cityId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetCityByIdQuery(cityId), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPost(Name = "CreateCity")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.CITY, CommandCode.CREATE)]
    public async Task<IActionResult> CreateCity([FromBody] CreateCityCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{cityId:guid}")]
    [ProducesResponseType(typeof(Result<CityDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.CITY, CommandCode.UPDATE)]
    public async Task<IActionResult> UpdateCity(Guid cityId, [FromBody] UpdateCityCommand command, CancellationToken cancellationToken)
    {
        command.SetId(cityId);
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{cityId:guid}/activate")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.CITY, CommandCode.ACTIVATE)]
    public async Task<IActionResult> ActivateCity(Guid cityId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new SetCityStatusCommand(cityId, ObjectStatus.Active), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{cityId:guid}/deactivate")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.CITY, CommandCode.INACTIVATE)]
    public async Task<IActionResult> DeactivateCity(Guid cityId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new SetCityStatusCommand(cityId, ObjectStatus.Inactive), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpDelete("{cityId:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.CITY, CommandCode.DELETE)]
    public async Task<IActionResult> DeleteCity(Guid cityId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteCityCommand(cityId), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }
}
