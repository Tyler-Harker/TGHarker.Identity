namespace TGharker.Identity.Web.Middleware;

public class TenantRequiredMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantRequiredMiddleware> _logger;

    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/tenants",
        "/account/login",
        "/account/logout",
        "/account/register",
        "/account/forgotpassword",
        "/account/resetpassword",
        "/account/confirmemail",
        "/health",
        "/alive",
        "/ready"
    };

    private static readonly string[] AllowedPathPrefixes =
    [
        "/.well-known/",
        "/connect/",
        "/api/",
        "/_framework/",
        "/_blazor/",
        "/lib/",
        "/css/",
        "/js/"
    ];

    public TenantRequiredMiddleware(RequestDelegate next, ILogger<TenantRequiredMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";

        // Skip middleware for allowed paths
        if (IsAllowedPath(path))
        {
            await _next(context);
            return;
        }

        // Skip for non-authenticated users (let auth middleware handle redirect to login)
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Check if user has a tenant_id claim (full session)
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantId))
        {
            await _next(context);
            return;
        }

        // User is authenticated but has no tenant - redirect to tenant selection
        var requiresTenantSelection = context.User.FindFirst("requires_tenant_selection")?.Value;
        if (requiresTenantSelection == "true")
        {
            _logger.LogDebug("User requires tenant selection, redirecting to /Tenants");
            context.Response.Redirect("/Tenants");
            return;
        }

        // Fallback - user authenticated but missing tenant claims, redirect to login
        _logger.LogWarning("User authenticated but missing expected claims, redirecting to login");
        context.Response.Redirect("/Account/Login");
    }

    private static bool IsAllowedPath(string path)
    {
        // Check exact matches
        if (AllowedPaths.Contains(path.TrimEnd('/')))
            return true;

        // Check static file extensions
        var extension = Path.GetExtension(path);
        if (!string.IsNullOrEmpty(extension))
        {
            return extension.Equals(".css", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".ico", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".woff", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".woff2", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".map", StringComparison.OrdinalIgnoreCase);
        }

        // Check path prefixes
        foreach (var prefix in AllowedPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

public static class TenantRequiredMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantRequired(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantRequiredMiddleware>();
    }
}
