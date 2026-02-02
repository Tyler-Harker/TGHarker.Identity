using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using TGHarker.Identity.Abstractions.Grains;

namespace TGharker.Identity.Web.Services;

/// <summary>
/// Service for checking user permissions within a tenant.
/// Permissions are derived from the user's roles in their tenant membership.
/// </summary>
public sealed class PermissionService : IPermissionService
{
    private readonly IClusterClient _clusterClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PermissionService> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public PermissionService(
        IClusterClient clusterClient,
        IMemoryCache cache,
        ILogger<PermissionService> logger)
    {
        _clusterClient = clusterClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permission)
    {
        var permissions = await GetPermissionsAsync(user);
        return permissions.Contains(permission);
    }

    public async Task<bool> HasAllPermissionsAsync(ClaimsPrincipal user, params string[] permissions)
    {
        var userPermissions = await GetPermissionsAsync(user);
        return permissions.All(p => userPermissions.Contains(p));
    }

    public async Task<bool> HasAnyPermissionAsync(ClaimsPrincipal user, params string[] permissions)
    {
        var userPermissions = await GetPermissionsAsync(user);
        return permissions.Any(p => userPermissions.Contains(p));
    }

    public async Task<IReadOnlySet<string>> GetPermissionsAsync(ClaimsPrincipal user)
    {
        var userId = user.FindFirst("sub")?.Value;
        var tenantId = user.FindFirst("tenant_id")?.Value;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning("Cannot get permissions: missing user ID or tenant ID");
            return new HashSet<string>();
        }

        var cacheKey = $"permissions:{tenantId}:{userId}";

        if (_cache.TryGetValue<IReadOnlySet<string>>(cacheKey, out var cachedPermissions) && cachedPermissions != null)
        {
            return cachedPermissions;
        }

        var permissions = await LoadPermissionsAsync(tenantId, userId);

        _cache.Set(cacheKey, permissions, CacheDuration);

        return permissions;
    }

    public async Task<bool> HasRoleAsync(ClaimsPrincipal user, string role)
    {
        var userId = user.FindFirst("sub")?.Value;
        var tenantId = user.FindFirst("tenant_id")?.Value;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId))
        {
            return false;
        }

        var membershipGrain = _clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{userId}");
        var membership = await membershipGrain.GetStateAsync();

        return membership?.Roles.Contains(role) == true;
    }

    private async Task<IReadOnlySet<string>> LoadPermissionsAsync(string tenantId, string userId)
    {
        var permissions = new HashSet<string>();

        try
        {
            // Get user's membership to find their roles
            var membershipGrain = _clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{userId}");
            var membership = await membershipGrain.GetStateAsync();

            if (membership == null || !membership.IsActive)
            {
                _logger.LogWarning("User {UserId} has no active membership in tenant {TenantId}", userId, tenantId);
                return permissions;
            }

            // Get tenant to access role definitions
            var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
            var roles = await tenantGrain.GetRolesAsync();

            // Collect all permissions from user's roles
            foreach (var roleId in membership.Roles)
            {
                var role = roles.FirstOrDefault(r => r.Id == roleId);
                if (role != null)
                {
                    foreach (var permission in role.Permissions)
                    {
                        permissions.Add(permission);
                    }
                }
                else
                {
                    _logger.LogWarning("Role {RoleId} not found in tenant {TenantId}", roleId, tenantId);
                }
            }

            _logger.LogDebug(
                "Loaded {PermissionCount} permissions for user {UserId} in tenant {TenantId} from roles: {Roles}",
                permissions.Count, userId, tenantId, string.Join(", ", membership.Roles));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load permissions for user {UserId} in tenant {TenantId}", userId, tenantId);
        }

        return permissions;
    }
}
