using Asp.Versioning;
using CommonServices.API.Abstractions;
using Infrastructure.Authorization;
using CommonServices.Application.Features.V1.Banks.Commands.CreateBank;
using CommonServices.Application.Features.V1.Banks.Commands.DeleteBank;
using CommonServices.Application.Features.V1.Banks.Commands.SetBankStatus;
using CommonServices.Application.Features.V1.Banks.Commands.UpdateBank;
using CommonServices.Application.Features.V1.Banks.Common.Dtos;
using CommonServices.Application.Features.V1.Banks.Queries.GetBankById;
using CommonServices.Application.Features.V1.Banks.Queries.GetBankList;
using CommonServices.Application.Features.V1.Banks.Queries.GetBanks;
using CommonServices.Domain.Enums;
using Contracts.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Common.Constants.Authorization;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.API.Controllers.V1;

[ApiVersion(1)]
public class BanksController : ApiController
{
    public BanksController(ISender sender) : base(sender)
    {
    }

    [HttpGet(Name = "GetBanks")]
    [ProducesResponseType(type: typeof(Result<PagedResult<BankDto>>), statusCode: StatusCodes.Status200OK)]
    [Permission(FunctionCode.BANK, CommandCode.VIEW)]
    public async Task<IActionResult> GetBanks(string? searchTerm = null,
        ObjectStatus? status = null,
        string? sortColumn = null,
        string? sortOrder = null,
        int pageIndex = PagedResult<BankDto>.DefaultPageIndex,
        int pageSize = PagedResult<BankDto>.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetBanksQuery(searchTerm, status, RequestLanguage, sortColumn,
            SortOrderExtension.ConvertStringToSortOrder(sortOrder),
            pageIndex,
            pageSize), cancellationToken);

        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("list")]
    [ProducesResponseType(type: typeof(Result<IReadOnlyList<BankDto>>), statusCode: StatusCodes.Status200OK)]
    [Permission(FunctionCode.BANK, CommandCode.VIEW)]
    public async Task<IActionResult> GetBankList(ObjectStatus? status = null, CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetBankListQuery(RequestLanguage, status), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("{bankId:guid}")]
    [ProducesResponseType(type: typeof(Result<BankDetailDto>), statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.BANK, CommandCode.VIEW)]
    public async Task<IActionResult> GetBankById(Guid bankId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetBankByIdQuery(bankId), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPost(Name = "CreateBank")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Permission(FunctionCode.BANK, CommandCode.CREATE)]
    public async Task<IActionResult> CreateBank([FromBody] CreateBankCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{bankId:guid}")]
    [ProducesResponseType(typeof(Result<BankDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.BANK, CommandCode.UPDATE)]
    public async Task<IActionResult> UpdateBank(Guid bankId, [FromBody] UpdateBankCommand command, CancellationToken cancellationToken)
    {
        command.SetId(bankId);
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{bankId:guid}/activate")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.BANK, CommandCode.ACTIVATE)]
    public async Task<IActionResult> ActivateBank(Guid bankId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new SetBankStatusCommand(bankId, ObjectStatus.Active), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{bankId:guid}/deactivate")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.BANK, CommandCode.INACTIVATE)]
    public async Task<IActionResult> DeactivateBank(Guid bankId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new SetBankStatusCommand(bankId, ObjectStatus.Inactive), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpDelete("{bankId:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.BANK, CommandCode.DELETE)]
    public async Task<IActionResult> DeleteBank(Guid bankId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteBankCommand(bankId), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }
}
