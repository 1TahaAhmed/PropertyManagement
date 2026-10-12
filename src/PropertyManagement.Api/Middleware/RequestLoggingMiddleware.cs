using System.Diagnostics;

namespace PropertyManagement.Api.Middleware;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        var requestId = context.TraceIdentifier;

        using var loggingScope = logger.BeginScope(
            new Dictionary<string, object>
            {
                ["RequestId"] = requestId,
                ["HttpMethod"] = context.Request.Method,
                ["Path"] = context.Request.Path.ToString()
            });

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            logger.LogInformation(
                "HTTP request completed. " +
                "StatusCode: {StatusCode}, " +
                "DurationMs: {DurationMs}",
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
