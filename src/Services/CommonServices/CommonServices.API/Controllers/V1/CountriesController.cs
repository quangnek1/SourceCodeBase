using Asp.Versioning;
using CommonServices.API.Abstractions;
using Infrastructure.Authorization;
using CommonServices.Application.Features.V1.Countries.Commands.CreateCountry;
using CommonServices.Application.Features.V1.Countries.Commands.DeleteCountry;
using CommonServices.Application.Features.V1.Countries.Commands.SetCountryStatus;
using CommonServices.Application.Features.V1.Countries.Commands.UpdateCountry;
using CommonServices.Application.Features.V1.Countries.Common.Dtos;
using CommonServices.Application.Features.V1.Countries.Queries.GetCountries;
using CommonServices.Application.Features.V1.Countries.Queries.GetCountryById;
using CommonServices.Application.Features.V1.Countries.Queries.GetCountryList;
using CommonServices.Domain.Enums;
using Contracts.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Common.Constants.Authorization;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.API.Controllers.V1;

[ApiVersion(1)]
public class CountriesController : ApiController
{
    public CountriesController(ISender sender) : base(sender)
    {
    }

    [HttpGet(Name = "GetCountries")]
    [ProducesResponseType(type: typeof(Result<PagedResult<CountryDto>>), statusCode: StatusCodes.Status200OK)]
    [Permission(FunctionCode.COUNTRY, CommandCode.VIEW)]
    public async Task<IActionResult> GetCountries(string? searchTerm = null,
        ObjectStatus? status = null,
        Guid? regionId = null,
        string? sortColumn = null,
        string? sortOrder = null,
        int pageIndex = PagedResult<CountryDto>.DefaultPageIndex,
        int pageSize = PagedResult<CountryDto>.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetCountriesQuery(searchTerm, status, regionId, RequestLanguage, sortColumn,
            SortOrderExtension.ConvertStringToSortOrder(sortOrder),
            pageIndex,
            pageSize), cancellationToken);

        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("list")]
    [ProducesResponseType(type: typeof(Result<IReadOnlyList<CountryDto>>), statusCode: StatusCodes.Status200OK)]
    [Permission(FunctionCode.COUNTRY, CommandCode.VIEW)]
    public async Task<IActionResult> GetCountryList(ObjectStatus? status = null,
        [FromQuery] Guid[]? regionIds = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetCountryListQuery(RequestLanguage, status, regionIds), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("{countryId:guid}")]
    [ProducesResponseType(type: typeof(Result<CountryDetailDto>), statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.COUNTRY, CommandCode.VIEW)]
    public async Task<IActionResult> GetCountryById(Guid countryId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetCountryByIdQuery(countryId), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPost(Name = "CreateCountry")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.COUNTRY, CommandCode.CREATE)]
    public async Task<IActionResult> CreateCountry([FromBody] CreateCountryCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{countryId:guid}")]
    [ProducesResponseType(typeof(Result<CountryDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.COUNTRY, CommandCode.UPDATE)]
    public async Task<IActionResult> UpdateCountry(Guid countryId, [FromBody] UpdateCountryCommand command, CancellationToken cancellationToken)
    {
        command.SetId(countryId);
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{countryId:guid}/activate")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.COUNTRY, CommandCode.ACTIVATE)]
    public async Task<IActionResult> ActivateCountry(Guid countryId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new SetCountryStatusCommand(countryId, ObjectStatus.Active), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{countryId:guid}/deactivate")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.COUNTRY, CommandCode.INACTIVATE)]
    public async Task<IActionResult> DeactivateCountry(Guid countryId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new SetCountryStatusCommand(countryId, ObjectStatus.Inactive), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpDelete("{countryId:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.COUNTRY, CommandCode.DELETE)]
    public async Task<IActionResult> DeleteCountry(Guid countryId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteCountryCommand(countryId), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }
}
