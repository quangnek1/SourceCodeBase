using AerationSterilize.API.Abstractions;
using Infrastructure.Authorization;
using AerationSterilize.Application.Features.V1.Permissions.Common.Dtos;
using AerationSterilize.Application.Features.V1.Permissions.Queries.GetPermissionMatrix;
using Asp.Versioning;
using Contracts.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Common.Constants.Authorization;

namespace AerationSterilize.API.Controllers.V1;

[ApiVersion(1)]
public class PermissionsController : ApiController
{
    public PermissionsController(ISender sender) : base(sender)
    {
    }

    // Rows (functions tree) and columns (actions) of the permission matrix, without any role's ticks.
    [HttpGet("matrix")]
    [ProducesResponseType(typeof(Result<PermissionMatrixDto>), StatusCodes.Status200OK)]
    [Permission(FunctionCode.ROLE, CommandCode.VIEW)]
    public async Task<IActionResult> GetPermissionMatrix(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetPermissionMatrixQuery(), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }
}
