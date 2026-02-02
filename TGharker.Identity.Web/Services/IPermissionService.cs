using System.Security.Claims;

namespace TGharker.Identity.Web.Services;

/// <summary>
/// Service for checking user permissions within a tenant.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Checks if the current user has a specific permission in their current tenant.
    /// </summary>
    Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permission);

    /// <summary>
    /// Checks if the current user has all of the specified permissions.
    /// </summary>
    Task<bool> HasAllPermissionsAsync(ClaimsPrincipal user, params string[] permissions);

    /// <summary>
    /// Checks if the current user has any of the specified permissions.
    /// </summary>
    Task<bool> HasAnyPermissionAsync(ClaimsPrincipal user, params string[] permissions);

    /// <summary>
    /// Gets all permissions for the current user in their current tenant.
    /// </summary>
    Task<IReadOnlySet<string>> GetPermissionsAsync(ClaimsPrincipal user);

    /// <summary>
    /// Checks if the current user has a specific role in their current tenant.
    /// </summary>
    Task<bool> HasRoleAsync(ClaimsPrincipal user, string role);
}
