using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace PropertyManagement.Api.Middleware;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Exception occurred. TraceId: {TraceId}",
            httpContext.TraceIdentifier);

        if (exception is DbUpdateConcurrencyException or DbUpdateException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

            var conflictDetails = new ProblemDetails
            {
                Type = "https://httpstatuses.com/409",
                Title = "Conflict / Concurrency Violation",
                Status = StatusCodes.Status409Conflict,
                Detail = "A conflict occurred with existing data or a concurrent modification. Please verify your data and try again.",
                Instance = httpContext.Request.Path
            };

            conflictDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = conflictDetails,
                Exception = exception
            });
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problemDetails = new ProblemDetails
        {
            Type = "https://httpstatuses.com/500",
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }
}