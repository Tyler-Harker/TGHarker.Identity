using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Middleware;

/// <summary>
/// Middleware that handles CORS for OAuth2/OIDC endpoints.
/// - Discovery endpoints (.well-known/*): Allows any origin (public metadata)
/// - OAuth endpoints (/connect/*): Validates origin against client's CorsOrigins
/// </summary>
public class OAuthCorsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OAuthCorsMiddleware> _logger;

    private static readonly string[] DiscoveryPaths =
    [
        "/.well-known/openid-configuration",
        "/.well-known/jwks.json"
    ];

    private static readonly string[] OAuthPaths =
    [
        "/connect/token",
        "/connect/userinfo",
        "/connect/revocation",
        "/connect/introspect"
    ];

    public OAuthCorsMiddleware(RequestDelegate next, ILogger<OAuthCorsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var origin = context.Request.Headers.Origin.FirstOrDefault();

        // Skip if not a CORS request
        if (string.IsNullOrEmpty(origin))
        {
            await _next(context);
            return;
        }

        // Check if this is a discovery endpoint (allow any origin)
        if (IsDiscoveryPath(path))
        {
            AddCorsHeaders(context, origin);

            if (IsPreflightRequest(context))
            {
                context.Response.StatusCode = 204;
                return;
            }

            await _next(context);
            return;
        }

        // Check if this is an OAuth endpoint
        if (IsOAuthPath(path))
        {
            var tenantResolver = context.RequestServices.GetRequiredService<ITenantResolver>();
            var corsService = context.RequestServices.GetRequiredService<IOAuthCorsService>();

            var tenant = await tenantResolver.ResolveAsync(context);
            if (tenant == null)
            {
                _logger.LogWarning("CORS request to {Path} without valid tenant", path);
                context.Response.StatusCode = 403;
                return;
            }

            var isAllowed = await corsService.IsOriginAllowedAsync(tenant.Id, origin);
            if (!isAllowed)
            {
                _logger.LogWarning("CORS origin {Origin} not allowed for tenant {TenantId}", origin, tenant.Id);
                context.Response.StatusCode = 403;
                return;
            }

            AddCorsHeaders(context, origin);

            if (IsPreflightRequest(context))
            {
                context.Response.StatusCode = 204;
                return;
            }
        }

        await _next(context);
    }

    private static bool IsDiscoveryPath(string path)
    {
        return DiscoveryPaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsOAuthPath(string path)
    {
        return OAuthPaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPreflightRequest(HttpContext context)
    {
        return context.Request.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddCorsHeaders(HttpContext context, string origin)
    {
        var headers = context.Response.Headers;

        headers["Access-Control-Allow-Origin"] = origin;
        headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
        headers["Access-Control-Allow-Credentials"] = "true";
        headers["Access-Control-Max-Age"] = "86400";
        headers["Vary"] = "Origin";
    }
}

public static class OAuthCorsMiddlewareExtensions
{
    public static IApplicationBuilder UseOAuthCors(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<OAuthCorsMiddleware>();
    }
}
