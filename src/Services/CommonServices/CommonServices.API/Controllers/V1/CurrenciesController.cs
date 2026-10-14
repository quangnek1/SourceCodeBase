using Asp.Versioning;
using CommonServices.API.Abstractions;
using CommonServices.API.Authorization;
using CommonServices.Application.Features.V1.Currencies.Commands.CreateCurrency;
using CommonServices.Application.Features.V1.Currencies.Commands.DeleteCurrency;
using CommonServices.Application.Features.V1.Currencies.Commands.SetCurrencyStatus;
using CommonServices.Application.Features.V1.Currencies.Commands.UpdateCurrency;
using CommonServices.Application.Features.V1.Currencies.Common.Dtos;
using CommonServices.Application.Features.V1.Currencies.Queries.GetCurrencies;
using CommonServices.Application.Features.V1.Currencies.Queries.GetCurrencyById;
using CommonServices.Application.Features.V1.Currencies.Queries.GetCurrencyList;
using CommonServices.Domain.Enums;
using Contracts.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Common.Constants.Authorization;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.API.Controllers.V1;

[ApiVersion(1)]
public class CurrenciesController : ApiController
{
    public CurrenciesController(ISender sender) : base(sender)
    {
    }

    [HttpGet(Name = "GetCurrencies")]
    [ProducesResponseType(type: typeof(Result<PagedResult<CurrencyDto>>), statusCode: StatusCodes.Status200OK)]
    [Permission(FunctionCode.CURRENCY, CommandCode.VIEW)]
    public async Task<IActionResult> GetCurrencies(string? searchTerm = null,
        ObjectStatus? status = null,
        string? sortColumn = null,
        string? sortOrder = null,
        int pageIndex = PagedResult<CurrencyDto>.DefaultPageIndex,
        int pageSize = PagedResult<CurrencyDto>.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetCurrenciesQuery(searchTerm, status, RequestLanguage, sortColumn,
            SortOrderExtension.ConvertStringToSortOrder(sortOrder),
            pageIndex,
            pageSize), cancellationToken);

        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("list")]
    [ProducesResponseType(type: typeof(Result<IReadOnlyList<CurrencyDto>>), statusCode: StatusCodes.Status200OK)]
    [Permission(FunctionCode.CURRENCY, CommandCode.VIEW)]
    public async Task<IActionResult> GetCurrencyList(ObjectStatus? status = null, CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetCurrencyListQuery(RequestLanguage, status), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("{currencyId:guid}")]
    [ProducesResponseType(type: typeof(Result<CurrencyDetailDto>), statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.CURRENCY, CommandCode.VIEW)]
    public async Task<IActionResult> GetCurrencyById(Guid currencyId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetCurrencyByIdQuery(currencyId), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPost(Name = "CreateCurrency")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Permission(FunctionCode.CURRENCY, CommandCode.CREATE)]
    public async Task<IActionResult> CreateCurrency([FromBody] CreateCurrencyCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{currencyId:guid}")]
    [ProducesResponseType(typeof(Result<CurrencyDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.CURRENCY, CommandCode.UPDATE)]
    public async Task<IActionResult> UpdateCurrency(Guid currencyId, [FromBody] UpdateCurrencyCommand command, CancellationToken cancellationToken)
    {
        command.SetId(currencyId);
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{currencyId:guid}/activate")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.CURRENCY, CommandCode.ACTIVATE)]
    public async Task<IActionResult> ActivateCurrency(Guid currencyId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new SetCurrencyStatusCommand(currencyId, ObjectStatus.Active), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{currencyId:guid}/deactivate")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.CURRENCY, CommandCode.INACTIVATE)]
    public async Task<IActionResult> DeactivateCurrency(Guid currencyId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new SetCurrencyStatusCommand(currencyId, ObjectStatus.Inactive), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpDelete("{currencyId:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.CURRENCY, CommandCode.DELETE)]
    public async Task<IActionResult> DeleteCurrency(Guid currencyId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteCurrencyCommand(currencyId), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }
}
