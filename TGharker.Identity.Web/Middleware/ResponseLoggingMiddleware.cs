namespace TGharker.Identity.Web.Middleware;

/// <summary>
/// Diagnostic middleware to log response details for OAuth endpoints.
/// This helps debug issues where clients receive unexpected content types.
/// </summary>
public class ResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ResponseLoggingMiddleware> _logger;

    public ResponseLoggingMiddleware(RequestDelegate next, ILogger<ResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";

        // Only log OAuth-related endpoints
        if (!path.Contains("/connect/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        _logger.LogInformation("OAuth Request: {Method} {Path} from {RemoteIp}",
            context.Request.Method,
            path,
            context.Connection.RemoteIpAddress);

        await _next(context);

        _logger.LogInformation("OAuth Response: {StatusCode} {ContentType} for {Path}",
            context.Response.StatusCode,
            context.Response.ContentType ?? "(not set)",
            path);
    }
}

public static class ResponseLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseResponseLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ResponseLoggingMiddleware>();
    }
}
