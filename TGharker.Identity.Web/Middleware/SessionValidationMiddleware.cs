using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TGHarker.Identity.Abstractions.Grains;

namespace TGharker.Identity.Web.Middleware;

/// <summary>
/// Middleware that validates the user's session by checking if the user and tenant still exist.
/// If the user no longer exists, their session is terminated.
/// If the tenant no longer exists, the user is redirected to tenant selection.
/// </summary>
public class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionValidationMiddleware> _logger;

    private static readonly HashSet<string> SkipPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/account/login",
        "/account/logout",
        "/account/register",
        "/account/forgotpassword",
        "/account/resetpassword",
        "/account/confirmemail",
        "/tenants",
        "/tenants/index",
        "/tenants/create",
        "/admin",  // SuperAdmin area root
        "/health",
        "/alive",
        "/ready"
    };

    private static readonly string[] SkipPathPrefixes =
    [
        "/tenant/",  // Tenant-specific auth pages
        "/admin/",   // SuperAdmin area (separate auth)
        "/.well-known/",
        "/connect/",
        "/api/",
        "/_framework/",
        "/_blazor/",
        "/lib/",
        "/css/",
        "/js/",
        "/images/"
    ];

    public SessionValidationMiddleware(RequestDelegate next, ILogger<SessionValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IClusterClient clusterClient)
    {
        var path = context.Request.Path.Value ?? "/";

        // Skip for paths that don't need validation
        if (ShouldSkip(path))
        {
            await _next(context);
            return;
        }

        // Skip for non-authenticated users
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? context.User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            await _next(context);
            return;
        }

        // Check if user still exists
        var userGrain = clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var userExists = await userGrain.ExistsAsync();

        if (!userExists)
        {
            _logger.LogWarning("User {UserId} no longer exists, terminating session", userId);
            await TerminateSessionAsync(context, "Your account no longer exists.");
            return;
        }

        // Check if user is still active
        var userState = await userGrain.GetStateAsync();
        if (userState == null || !userState.IsActive)
        {
            _logger.LogWarning("User {UserId} is no longer active, terminating session", userId);
            await TerminateSessionAsync(context, "Your account has been deactivated.");
            return;
        }

        // Check if tenant still exists (if user has a tenant selected)
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantId))
        {
            var tenantGrain = clusterClient.GetGrain<ITenantGrain>(tenantId);
            var tenantExists = await tenantGrain.ExistsAsync();

            if (!tenantExists)
            {
                _logger.LogWarning("Tenant {TenantId} no longer exists for user {UserId}, redirecting to tenant selection", tenantId, userId);
                await RedirectToTenantSelectionAsync(context, "The organization you were signed into no longer exists. Please select another organization.");
                return;
            }

            // Check if tenant is still active
            var tenantState = await tenantGrain.GetStateAsync();
            if (tenantState == null || !tenantState.IsActive)
            {
                _logger.LogWarning("Tenant {TenantId} is no longer active for user {UserId}, redirecting to tenant selection", tenantId, userId);
                await RedirectToTenantSelectionAsync(context, "The organization you were signed into has been deactivated. Please select another organization.");
                return;
            }

            // Check if user is still a member of this tenant
            var membershipGrain = clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{userId}");
            var membershipState = await membershipGrain.GetStateAsync();

            if (membershipState == null || !membershipState.IsActive)
            {
                _logger.LogWarning("User {UserId} is no longer a member of tenant {TenantId}, redirecting to tenant selection", userId, tenantId);
                await RedirectToTenantSelectionAsync(context, "You are no longer a member of the organization you were signed into. Please select another organization.");
                return;
            }
        }

        await _next(context);
    }

    private static bool ShouldSkip(string path)
    {
        // Check exact matches
        if (SkipPaths.Contains(path.TrimEnd('/')))
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
        foreach (var prefix in SkipPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static async Task TerminateSessionAsync(HttpContext context, string message)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Store message in a cookie so login page can display it
        context.Response.Cookies.Append("session_message", message, new CookieOptions
        {
            HttpOnly = false,
            Expires = DateTimeOffset.UtcNow.AddMinutes(5),
            SameSite = SameSiteMode.Lax
        });

        context.Response.Redirect("/Account/Login");
    }

    private static Task RedirectToTenantSelectionAsync(HttpContext context, string message)
    {
        // Store message in a cookie so tenant selection page can display it
        context.Response.Cookies.Append("session_message", message, new CookieOptions
        {
            HttpOnly = false,
            Expires = DateTimeOffset.UtcNow.AddMinutes(5),
            SameSite = SameSiteMode.Lax
        });

        context.Response.Redirect("/Tenants");
        return Task.CompletedTask;
    }
}

public static class SessionValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SessionValidationMiddleware>();
    }
}
