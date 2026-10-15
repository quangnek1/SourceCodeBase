using AerationSterilize.API.Abstractions;
using Infrastructure.Authorization;
using AerationSterilize.Application.Features.V1.Permissions.Commands.UpdateRolePermissions;
using AerationSterilize.Application.Features.V1.Permissions.Common.Dtos;
using AerationSterilize.Application.Features.V1.Permissions.Queries.GetRolePermissions;
using AerationSterilize.Application.Features.V1.Roles.Commands.CreateRole;
using AerationSterilize.Application.Features.V1.Roles.Commands.DeleteRole;
using AerationSterilize.Application.Features.V1.Roles.Commands.UpdateRole;
using AerationSterilize.Application.Features.V1.Roles.Common.Dtos;
using AerationSterilize.Application.Features.V1.Roles.Queries.GetRoleById;
using AerationSterilize.Application.Features.V1.Roles.Queries.GetRoles;
using Asp.Versioning;
using Contracts.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Common.Constants.Authorization;

namespace AerationSterilize.API.Controllers.V1;

[ApiVersion(1)]
public class RolesController : ApiController
{
    public RolesController(ISender sender) : base(sender)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<IReadOnlyList<RoleDto>>), StatusCodes.Status200OK)]
    [Permission(FunctionCode.ROLE, CommandCode.VIEW)]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetRolesQuery(), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("{roleId:guid}")]
    [ProducesResponseType(typeof(Result<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.ROLE, CommandCode.VIEW)]
    public async Task<IActionResult> GetRoleById(Guid roleId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetRoleByIdQuery(roleId), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Permission(FunctionCode.ROLE, CommandCode.CREATE)]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{roleId:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.ROLE, CommandCode.UPDATE)]
    public async Task<IActionResult> UpdateRole(Guid roleId, [FromBody] UpdateRoleCommand command, CancellationToken cancellationToken)
    {
        command.SetId(roleId);
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpDelete("{roleId:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.ROLE, CommandCode.DELETE)]
    public async Task<IActionResult> DeleteRole(Guid roleId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteRoleCommand(roleId), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    // The ticked cells of the role's permission matrix.
    [HttpGet("{roleId:guid}/permissions")]
    [ProducesResponseType(typeof(Result<IReadOnlyList<RolePermissionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.ROLE, CommandCode.VIEW)]
    public async Task<IActionResult> GetRolePermissions(Guid roleId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetRolePermissionsQuery(roleId), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    // Saves the whole matrix of the role at once.
    [HttpPut("{roleId:guid}/permissions")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.ROLE, CommandCode.UPDATE)]
    public async Task<IActionResult> UpdateRolePermissions(Guid roleId, [FromBody] UpdateRolePermissionsCommand command, CancellationToken cancellationToken)
    {
        command.SetRoleId(roleId);
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }
}
