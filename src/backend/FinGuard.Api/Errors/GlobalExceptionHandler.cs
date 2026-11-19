using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace FinGuard.Api.Errors;

// Unexpected faults: same generic 500 in every environment, no message, no stack trace (D-35). Requests the server
// rejected while reading them (BadHttpRequestException, e.g. 413) keep their status with a generic title.
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        // Rejected requests (body over the limit, unreadable form) are client errors with a known status, e.g. 413 (D-88).
        var (status, title) = exception is BadHttpRequestException badRequest
            ? (badRequest.StatusCode, ReasonPhrases.GetReasonPhrase(badRequest.StatusCode))
            : (StatusCodes.Status500InternalServerError, "An unexpected error occurred.");

        if (status == StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, exception.GetType().FullName, traceId);
        }
        else
        {
            LogRejected(logger, status, traceId);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = status, Title = title },
        });
    }

    // The exception object is deliberately not passed: its message may carry data that must not be logged.
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception {ExceptionType} (trace {TraceId})")]
    private static partial void LogUnhandled(ILogger logger, string? exceptionType, string traceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected request with status {StatusCode} (trace {TraceId})")]
    private static partial void LogRejected(ILogger logger, int statusCode, string traceId);
}
