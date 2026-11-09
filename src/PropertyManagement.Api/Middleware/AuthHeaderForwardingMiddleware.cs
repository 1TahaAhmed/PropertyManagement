namespace PropertyManagement.Api.Middleware;

public sealed class AuthHeaderForwardingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.ContainsKey("Authorization"))
        {
            if (context.Request.Headers.TryGetValue("X-Authorization", out var xAuth) && !string.IsNullOrWhiteSpace(xAuth))
            {
                var tokenValue = xAuth.ToString().Trim();
                if (!tokenValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    tokenValue = $"Bearer {tokenValue}";
                }
                context.Request.Headers["Authorization"] = tokenValue;
            }
        }

        await next(context);
    }
}
