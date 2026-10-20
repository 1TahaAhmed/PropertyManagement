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

        var isUniqueViolation = exception is DbUpdateException dbUpdateEx &&
                                dbUpdateEx.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx &&
                                (sqlEx.Number == 2601 || sqlEx.Number == 2627);

        if (exception is DbUpdateConcurrencyException || isUniqueViolation)
        {
            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

            var conflictDetails = new ProblemDetails
            {
                Type = "https://httpstatuses.com/409",
                Title = "Conflict Detected",
                Status = StatusCodes.Status409Conflict,
                Detail = isUniqueViolation
                    ? "A record with the same unique identifier or name already exists."
                    : "The resource was modified by another user. Please refresh and try again.",
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