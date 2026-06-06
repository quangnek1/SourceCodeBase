using System.Text.Json;
using Contracts.Exceptions;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Infrastructure.Middlewares;

public sealed class ExceptionHandlingMiddleware : IMiddleware
{
    private readonly ILogger _logger;

    public ExceptionHandlingMiddleware(ILogger logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unhandled exception: {Message}", ex.Message);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = new ErrorResponse
        {
            Title = GetTitle(exception),
            Status = GetStatusCode(exception),
            Detail = exception.Message,
            Errors = GetErrors(exception)
        };

        context.Response.ContentType = "application/json";

        context.Response.StatusCode = response.Status;

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private static int GetStatusCode(Exception exception)
    {
        return exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,

            BadRequestException => StatusCodes.Status400BadRequest,

            NotFoundException => StatusCodes.Status404NotFound,

            FormatException => StatusCodes.Status422UnprocessableEntity,

            _ => StatusCodes.Status500InternalServerError
        };
    }

    private static string GetTitle(
        Exception exception)
    {
        return exception switch
        {
            DomainException domainException => domainException.Title,
            _ => "Internal Server Error"
        };
    }

    private static IReadOnlyCollection<ValidationError>?
        GetErrors(Exception exception)
    {
        return exception switch
        {
            ValidationException validationException => validationException.Errors,
            _ => null
        };
    }
}

