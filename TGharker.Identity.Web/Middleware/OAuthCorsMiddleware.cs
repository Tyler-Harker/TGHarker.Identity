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
        var isOAuthPath = IsOAuthPath(path);
        var isDiscoveryPath = IsDiscoveryPath(path);

        // Diagnostic logging for OAuth endpoints
        if (isOAuthPath || isDiscoveryPath)
        {
            _logger.LogInformation(
                "[CORS Diagnostics] Request to {Path} | Method: {Method} | Origin: {Origin} | Has-Origin-Header: {HasOrigin}",
                path,
                context.Request.Method,
                origin ?? "(null)",
                !string.IsNullOrEmpty(origin));

            // Log all request headers for debugging
            var headers = string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}={h.Value}"));
            _logger.LogDebug("[CORS Diagnostics] All headers: {Headers}", headers);
        }

        // Skip if not a CORS request
        if (string.IsNullOrEmpty(origin))
        {
            if (isOAuthPath || isDiscoveryPath)
            {
                _logger.LogWarning(
                    "[CORS Diagnostics] No Origin header present for {Path} - skipping CORS handling. " +
                    "This may indicate the header was stripped by a proxy/load balancer.",
                    path);
            }
            await _next(context);
            return;
        }

        // Check if this is a discovery endpoint (allow any origin)
        if (isDiscoveryPath)
        {
            _logger.LogInformation("[CORS Diagnostics] Discovery endpoint - allowing origin {Origin}", origin);
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
        if (isOAuthPath)
        {
            var tenantResolver = context.RequestServices.GetRequiredService<ITenantResolver>();
            var corsService = context.RequestServices.GetRequiredService<IOAuthCorsService>();

            var tenant = await tenantResolver.ResolveAsync(context);
            if (tenant == null)
            {
                _logger.LogWarning("[CORS Diagnostics] CORS request to {Path} without valid tenant - rejecting", path);
                context.Response.StatusCode = 403;
                return;
            }

            _logger.LogInformation("[CORS Diagnostics] Checking if origin {Origin} is allowed for tenant {TenantId}", origin, tenant.Id);

            var allowedOrigins = await corsService.GetAllowedOriginsAsync(tenant.Id);
            _logger.LogInformation("[CORS Diagnostics] Allowed origins for tenant {TenantId}: {AllowedOrigins}",
                tenant.Id,
                string.Join(", ", allowedOrigins));

            var isAllowed = await corsService.IsOriginAllowedAsync(tenant.Id, origin);
            if (!isAllowed)
            {
                _logger.LogWarning(
                    "[CORS Diagnostics] Origin {Origin} NOT ALLOWED for tenant {TenantId}. " +
                    "Add this origin to the client's CorsOrigins configuration.",
                    origin, tenant.Id);
                context.Response.StatusCode = 403;
                return;
            }

            _logger.LogInformation("[CORS Diagnostics] Origin {Origin} allowed - adding CORS headers", origin);
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
        // Check exact match or tenant-prefixed match (e.g., /{tenantId}/.well-known/...)
        return DiscoveryPaths.Any(p =>
            path.Equals(p, StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsOAuthPath(string path)
    {
        // Check exact match or tenant-prefixed match (e.g., /{tenantId}/connect/...)
        return OAuthPaths.Any(p =>
            path.Equals(p, StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(p, StringComparison.OrdinalIgnoreCase));
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
