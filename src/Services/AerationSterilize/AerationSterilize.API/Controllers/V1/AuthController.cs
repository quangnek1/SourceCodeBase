using System.Threading;
using AerationSterilize.API.Abstractions;
using AerationSterilize.API.Authorization;
using AerationSterilize.Application.Features.V1.Auth.Commands.Login;
using AerationSterilize.Application.Features.V1.Auth.Commands.Logout;
using AerationSterilize.Application.Features.V1.Auth.Commands.RefreshToken;
using AerationSterilize.Application.Features.V1.Auth.Commands.Register;
using AerationSterilize.Application.Features.V1.Auth.Queries.GetMe;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Common.Constants.Authorization;

namespace AerationSterilize.API.Controllers.V1;

[ApiVersion(1)]
public class AuthController : ApiController
{
    public AuthController(ISender sender) : base(sender)
    {
    }

    [HttpPost]
    [Route("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Login([FromForm] LoginCommand command)
    {
        var result = await Sender.Send(command);
        if (result.IsFailure)
            return HandleFailure(result);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(30)
        };

        Response.Cookies.Append("refreshToken", result.Value.RefreshToken, cookieOptions);

        return Ok(new
        {
            accessToken = result.Value.AccessToken,
            expiresIn = result.Value.ExpiresIn
        });
    }

    // Accounts are created by an administrator; open self-registration is not allowed.
    [HttpPost]
    [Route("register")]
    [Permission(FunctionCode.USER, CommandCode.CREATE)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);

        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPost]
    [Route("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (refreshToken == null)
            return Unauthorized();

        var command = new RefreshTokenCommand(refreshToken);
        var result = await Sender.Send(command, cancellationToken);

        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPost]
    [Route("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (refreshToken == null)
            return Unauthorized();

        var command = new LogoutCommand(refreshToken);
        var result = await Sender.Send(command, cancellationToken);

        Response.Cookies.Delete("refreshToken");

        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet]
    [Route("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetMeQuery(), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }
}
