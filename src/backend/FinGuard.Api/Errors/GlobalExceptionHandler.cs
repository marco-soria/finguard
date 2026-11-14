using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FinGuard.Api.Errors;

// Unexpected faults only. Same generic 500 in every environment: no message, no stack trace (D-35).
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        LogUnhandled(logger, exception.GetType().FullName, traceId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
            },
        });
    }

    // The exception object is deliberately not passed: its message may carry data that must not be logged.
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception {ExceptionType} (trace {TraceId})")]
    private static partial void LogUnhandled(ILogger logger, string? exceptionType, string traceId);
}
