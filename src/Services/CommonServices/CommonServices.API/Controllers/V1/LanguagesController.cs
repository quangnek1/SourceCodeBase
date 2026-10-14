using Asp.Versioning;
using CommonServices.API.Abstractions;
using CommonServices.API.Authorization;
using CommonServices.Application.Features.V1.Languages.Commands.CreateLanguage;
using CommonServices.Application.Features.V1.Languages.Commands.DeleteLanguage;
using CommonServices.Application.Features.V1.Languages.Commands.SetLanguageStatus;
using CommonServices.Application.Features.V1.Languages.Commands.UpdateLanguage;
using CommonServices.Application.Features.V1.Languages.Common.Dtos;
using CommonServices.Application.Features.V1.Languages.Queries.GetLanguageById;
using CommonServices.Application.Features.V1.Languages.Queries.GetLanguageList;
using CommonServices.Application.Features.V1.Languages.Queries.GetLanguages;
using CommonServices.Domain.Enums;
using Contracts.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Common.Constants.Authorization;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.API.Controllers.V1;

[ApiVersion(1)]
public class LanguagesController : ApiController
{
    public LanguagesController(ISender sender) : base(sender)
    {
    }

    [HttpGet(Name = "GetLanguages")]
    [ProducesResponseType(type: typeof(Result<PagedResult<LanguageDto>>), statusCode: StatusCodes.Status200OK)]
    [Permission(FunctionCode.LANGUAGE, CommandCode.VIEW)]
    public async Task<IActionResult> GetLanguages(string? searchTerm = null,
        ObjectStatus? status = null,
        string? sortColumn = null,
        string? sortOrder = null,
        int pageIndex = PagedResult<LanguageDto>.DefaultPageIndex,
        int pageSize = PagedResult<LanguageDto>.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetLanguagesQuery(searchTerm, status, RequestLanguage, sortColumn,
            SortOrderExtension.ConvertStringToSortOrder(sortOrder),
            pageIndex,
            pageSize), cancellationToken);

        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("list")]
    [ProducesResponseType(type: typeof(Result<IReadOnlyList<LanguageDto>>), statusCode: StatusCodes.Status200OK)]
    [Permission(FunctionCode.LANGUAGE, CommandCode.VIEW)]
    public async Task<IActionResult> GetLanguageList(ObjectStatus? status = null, CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetLanguageListQuery(RequestLanguage, status), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("{languageId:guid}")]
    [ProducesResponseType(type: typeof(Result<LanguageDetailDto>), statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.LANGUAGE, CommandCode.VIEW)]
    public async Task<IActionResult> GetLanguageById(Guid languageId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetLanguageByIdQuery(languageId), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPost(Name = "CreateLanguage")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Permission(FunctionCode.LANGUAGE, CommandCode.CREATE)]
    public async Task<IActionResult> CreateLanguage([FromBody] CreateLanguageCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{languageId:guid}")]
    [ProducesResponseType(typeof(Result<LanguageDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.LANGUAGE, CommandCode.UPDATE)]
    public async Task<IActionResult> UpdateLanguage(Guid languageId, [FromBody] UpdateLanguageCommand command, CancellationToken cancellationToken)
    {
        command.SetId(languageId);
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{languageId:guid}/activate")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.LANGUAGE, CommandCode.ACTIVATE)]
    public async Task<IActionResult> ActivateLanguage(Guid languageId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new SetLanguageStatusCommand(languageId, ObjectStatus.Active), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{languageId:guid}/deactivate")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.LANGUAGE, CommandCode.INACTIVATE)]
    public async Task<IActionResult> DeactivateLanguage(Guid languageId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new SetLanguageStatusCommand(languageId, ObjectStatus.Inactive), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpDelete("{languageId:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.LANGUAGE, CommandCode.DELETE)]
    public async Task<IActionResult> DeleteLanguage(Guid languageId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteLanguageCommand(languageId), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }
}
