using CommonServices.Domain.Constants;
using Contracts.Responses;
using Contracts.Responses.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CommonServices.API.Abstractions;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class ApiController : ControllerBase
{
    protected readonly ISender Sender;

    protected ApiController(ISender sender)
        => Sender = sender;

    // Language of the translated names, taken from Accept-Language like the legacy common-services ("en-US,en;q=0.9" => "en-US").
    protected string RequestLanguage
    {
        get
        {
            var header = Request.Headers.AcceptLanguage.ToString();
            var language = header.Split(',', ';')[0].Trim();

            return string.IsNullOrEmpty(language) ? LanguageCodes.Default : language;
        }
    }

    // A failed Result must not be passed to Ok(): serializing it reads Result<T>.Value, which throws on failure.
    protected IActionResult HandleFailure(Result result)
        => result switch
        {
            { IsSuccess: true } => throw new InvalidOperationException("Cannot handle a successful result as a failure."),
            IValidationResult validationResult => BadRequest(CreateProblemDetails(
                "Validation Error", StatusCodes.Status400BadRequest, result.Error, validationResult.Errors)),
            _ => BadRequest(CreateProblemDetails(
                "Bad Request", StatusCodes.Status400BadRequest, result.Error))
        };

    private static ProblemDetails CreateProblemDetails(string title, int status, Error error, Error[]? errors = null)
        => new()
        {
            Title = title,
            Type = error.Code,
            Detail = error.Message,
            Status = status,
            Extensions = { { nameof(errors), errors } }
        };
}
