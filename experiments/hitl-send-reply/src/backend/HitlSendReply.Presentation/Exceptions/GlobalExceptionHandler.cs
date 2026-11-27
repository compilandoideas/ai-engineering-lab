using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HitlSendReply.Presentation.Exceptions;

/// <summary>
/// Catches unhandled exceptions that escape the pipeline (model binding, middleware) and turns
/// them into RFC 9110 Problem Details. The ExceptionHandlingDecorator in Application covers the
/// exceptions inside a handler; this covers the rest.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        ProblemDetails problemDetails = MapException(exception);
        httpContext.Response.StatusCode = problemDetails.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static ProblemDetails MapException(Exception exception) =>
        IsJsonDeserializationFailure(exception)
            ? new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid request body",
                Type = "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
            }
            : new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Server failure",
                Type = "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1",
            };

    // Minimal API model binding wraps malformed JSON in a BadHttpRequestException whose inner
    // exception is the JsonException, so the chain is walked.
    private static bool IsJsonDeserializationFailure(Exception exception)
    {
        for (Exception? e = exception; e is not null; e = e.InnerException)
        {
            if (e is JsonException)
            {
                return true;
            }
        }

        return false;
    }
}
