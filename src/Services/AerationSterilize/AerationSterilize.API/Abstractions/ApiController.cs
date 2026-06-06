using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AerationSterilize.API.Abstractions;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class ApiController : ControllerBase
{
    protected readonly ISender Sender;

    protected ApiController(ISender sender)
        => Sender = sender;
}
