using AerationSterilize.API.Abstractions;
using AerationSterilize.API.Authorization;
using AerationSterilize.Application.Features.V1.Users.Commands.AssignUserRoles;
using AerationSterilize.Application.Features.V1.Users.Common.Dtos;
using AerationSterilize.Application.Features.V1.Users.Queries.GetUsers;
using Asp.Versioning;
using Contracts.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Common.Constants.Authorization;
using Shared.Paging;

namespace AerationSterilize.API.Controllers.V1;

[ApiVersion(1)]
public class UsersController : ApiController
{
    public UsersController(ISender sender) : base(sender)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<PagedResult<UserSummaryDto>>), StatusCodes.Status200OK)]
    [Permission(FunctionCode.USER, CommandCode.VIEW)]
    public async Task<IActionResult> GetUsers(
        string? searchTerm = null,
        int pageIndex = PagedResult<UserSummaryDto>.DefaultPageIndex,
        int pageSize = PagedResult<UserSummaryDto>.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetUsersQuery(searchTerm, pageIndex, pageSize), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    // Replaces the user's roles.
    [HttpPut("{userId:guid}/roles")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.USER, CommandCode.UPDATE)]
    public async Task<IActionResult> AssignRoles(Guid userId, [FromBody] AssignUserRolesCommand command, CancellationToken cancellationToken)
    {
        command.SetUserId(userId);
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }
}
