using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGharker.Identity.Web.Endpoints;

public static class ApplicationRolesEndpoint
{
    public static IEndpointRouteBuilder MapApplicationRolesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Permission management
        endpoints.MapGet("/api/clients/{clientId}/permissions", GetPermissionsAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("GetApplicationPermissions")
            .WithTags("Application Roles");

        endpoints.MapPost("/api/clients/{clientId}/permissions", AddPermissionAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("AddApplicationPermission")
            .WithTags("Application Roles");

        endpoints.MapDelete("/api/clients/{clientId}/permissions/{name}", RemovePermissionAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("RemoveApplicationPermission")
            .WithTags("Application Roles");

        // Role management
        endpoints.MapGet("/api/clients/{clientId}/roles", GetRolesAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("GetApplicationRoles")
            .WithTags("Application Roles");

        endpoints.MapPost("/api/clients/{clientId}/roles", CreateRoleAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("CreateApplicationRole")
            .WithTags("Application Roles");

        endpoints.MapPut("/api/clients/{clientId}/roles/{roleId}", UpdateRoleAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("UpdateApplicationRole")
            .WithTags("Application Roles");

        endpoints.MapDelete("/api/clients/{clientId}/roles/{roleId}", DeleteRoleAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("DeleteApplicationRole")
            .WithTags("Application Roles");

        // User role assignment
        endpoints.MapGet("/api/clients/{clientId}/users/{userId}/roles", GetUserRolesAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("GetUserApplicationRoles")
            .WithTags("Application Roles");

        endpoints.MapPost("/api/clients/{clientId}/users/{userId}/roles", AssignUserRoleAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("AssignUserApplicationRole")
            .WithTags("Application Roles");

        endpoints.MapDelete("/api/clients/{clientId}/users/{userId}/roles/{roleId}", RemoveUserRoleAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("RemoveUserApplicationRole")
            .WithTags("Application Roles");

        // System permissions/roles sync (for application self-registration via CCF)
        endpoints.MapPut("/api/clients/{clientId}/system/permissions", SyncSystemPermissionsAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("SyncSystemPermissions")
            .WithTags("Application Roles");

        endpoints.MapPut("/api/clients/{clientId}/system/roles", SyncSystemRolesAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("SyncSystemRoles")
            .WithTags("Application Roles");

        // Tenant-prefixed routes
        MapTenantPrefixedRoutes(endpoints);

        return endpoints;
    }

    private static void MapTenantPrefixedRoutes(IEndpointRouteBuilder endpoints)
    {
        var basePath = "/tenant/{tenantId}/api/clients/{clientId}";

        // Permission management
        endpoints.MapGet($"{basePath}/permissions", GetPermissionsWithTenantAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("GetApplicationPermissionsWithTenant")
            .WithTags("Application Roles");

        endpoints.MapPost($"{basePath}/permissions", AddPermissionWithTenantAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("AddApplicationPermissionWithTenant")
            .WithTags("Application Roles");

        endpoints.MapDelete($"{basePath}/permissions/{{name}}", RemovePermissionWithTenantAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("RemoveApplicationPermissionWithTenant")
            .WithTags("Application Roles");

        // Role management
        endpoints.MapGet($"{basePath}/roles", GetRolesWithTenantAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("GetApplicationRolesWithTenant")
            .WithTags("Application Roles");

        endpoints.MapPost($"{basePath}/roles", CreateRoleWithTenantAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("CreateApplicationRoleWithTenant")
            .WithTags("Application Roles");

        endpoints.MapPut($"{basePath}/roles/{{roleId}}", UpdateRoleWithTenantAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("UpdateApplicationRoleWithTenant")
            .WithTags("Application Roles");

        endpoints.MapDelete($"{basePath}/roles/{{roleId}}", DeleteRoleWithTenantAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("DeleteApplicationRoleWithTenant")
            .WithTags("Application Roles");

        // User role assignment
        endpoints.MapGet($"{basePath}/users/{{userId}}/roles", GetUserRolesWithTenantAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("GetUserApplicationRolesWithTenant")
            .WithTags("Application Roles");

        endpoints.MapPost($"{basePath}/users/{{userId}}/roles", AssignUserRoleWithTenantAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("AssignUserApplicationRoleWithTenant")
            .WithTags("Application Roles");

        endpoints.MapDelete($"{basePath}/users/{{userId}}/roles/{{roleId}}", RemoveUserRoleWithTenantAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("RemoveUserApplicationRoleWithTenant")
            .WithTags("Application Roles");
    }

    #region Permission Endpoints

    private static async Task<IResult> GetPermissionsAsync(
        string clientId,
        HttpContext context,
        IClusterClient clusterClient)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantId))
            return Results.Unauthorized();

        return await GetPermissionsInternalAsync(tenantId, clientId, context, clusterClient);
    }

    private static async Task<IResult> GetPermissionsWithTenantAsync(
        string tenantId,
        string clientId,
        HttpContext context,
        IClusterClient clusterClient)
    {
        return await GetPermissionsInternalAsync(tenantId, clientId, context, clusterClient);
    }

    private static async Task<IResult> GetPermissionsInternalAsync(
        string tenantId,
        string clientId,
        HttpContext context,
        IClusterClient clusterClient)
    {
        if (!await IsAdminAsync(context, clusterClient, tenantId))
            return Results.Forbid();

        var clientGrain = clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        if (!await clientGrain.ExistsAsync())
            return Results.NotFound(new { error = "client_not_found" });

        var permissions = await clientGrain.GetApplicationPermissionsAsync();
        return Results.Ok(new ApplicationPermissionsResponse { Permissions = permissions.ToList() });
    }

    private static async Task<IResult> AddPermissionAsync(
        string clientId,
        AddPermissionRequest request,
        HttpContext context,
        IClusterClient clusterClient)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantId))
            return Results.Unauthorized();

        return await AddPermissionInternalAsync(tenantId, clientId, request, context, clusterClient);
    }

    private static async Task<IResult> AddPermissionWithTenantAsync(
        string tenantId,
        string clientId,
        AddPermissionRequest request,
        HttpContext context,
        IClusterClient clusterClient)
    {
        return await AddPermissionInternalAsync(tenantId, clientId, request, context, clusterClient);
    }

    private static async Task<IResult> AddPermissionInternalAsync(
        string tenantId,
        string clientId,
        AddPermissionRequest request,
        HttpContext context,
        IClusterClient clusterClient)
    {
        if (!await IsAdminAsync(context, clusterClient, tenantId))
            return Results.Forbid();

        var clientGrain = clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        if (!await clientGrain.ExistsAsync())
            return Results.NotFound(new { error = "client_not_found" });

        var permission = await clientGrain.AddPermissionAsync(request.Name, request.DisplayName, request.Description);
        return Results.Created($"/api/clients/{clientId}/permissions/{permission.Name}", permission);
    }

    private static async Task<IResult> RemovePermissionAsync(
        string clientId,
        string name,
        HttpContext context,
        IClusterClient clusterClient)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantId))
            return Results.Unauthorized();

        return await RemovePermissionInternalAsync(tenantId, clientId, name, context, clusterClient);
    }

    private static async Task<IResult> RemovePermissionWithTenantAsync(
        string tenantId,
        string clientId,
        string name,
        HttpContext context,
        IClusterClient clusterClient)
    {
        return await RemovePermissionInternalAsync(tenantId, clientId, name, context, clusterClient);
    }

    private static async Task<IResult> RemovePermissionInternalAsync(
        string tenantId,
        string clientId,
        string name,
        HttpContext context,
        IClusterClient clusterClient)
    {
        if (!await IsAdminAsync(context, clusterClient, tenantId))
            return Results.Forbid();

        var clientGrain = clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        if (!await clientGrain.ExistsAsync())
            return Results.NotFound(new { error = "client_not_found" });

        var removed = await clientGrain.RemovePermissionAsync(name);
        if (!removed)
            return Results.NotFound(new { error = "permission_not_found" });

        return Results.NoContent();
    }

    #endregion

    #region Role Endpoints

    private static async Task<IResult> GetRolesAsync(
        string clientId,
        HttpContext context,
        IClusterClient clusterClient)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantId))
            return Results.Unauthorized();

        return await GetRolesInternalAsync(tenantId, clientId, context, clusterClient);
    }

    private static async Task<IResult> GetRolesWithTenantAsync(
        string tenantId,
        string clientId,
        HttpContext context,
        IClusterClient clusterClient)
    {
        return await GetRolesInternalAsync(tenantId, clientId, context, clusterClient);
    }

    private static async Task<IResult> GetRolesInternalAsync(
        string tenantId,
        string clientId,
        HttpContext context,
        IClusterClient clusterClient)
    {
        if (!await IsAdminAsync(context, clusterClient, tenantId))
            return Results.Forbid();

        var clientGrain = clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        if (!await clientGrain.ExistsAsync())
            return Results.NotFound(new { error = "client_not_found" });

        var roles = await clientGrain.GetApplicationRolesAsync();
        return Results.Ok(new ApplicationRolesResponse { Roles = roles.ToList() });
    }

    private static async Task<IResult> CreateRoleAsync(
        string clientId,
        CreateRoleRequest request,
        HttpContext context,
        IClusterClient clusterClient)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantId))
            return Results.Unauthorized();

        return await CreateRoleInternalAsync(tenantId, clientId, request, context, clusterClient);
    }

    private static async Task<IResult> CreateRoleWithTenantAsync(
        string tenantId,
        string clientId,
        CreateRoleRequest request,
        HttpContext context,
        IClusterClient clusterClient)
    {
        return await CreateRoleInternalAsync(tenantId, clientId, request, context, clusterClient);
    }

    private static async Task<IResult> CreateRoleInternalAsync(
        string tenantId,
        string clientId,
        CreateRoleRequest request,
        HttpContext context,
        IClusterClient clusterClient)
    {
        if (!await IsAdminAsync(context, clusterClient, tenantId))
            return Results.Forbid();

        var clientGrain = clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        if (!await clientGrain.ExistsAsync())
            return Results.NotFound(new { error = "client_not_found" });

        try
        {
            var role = await clientGrain.CreateApplicationRoleAsync(
                request.Name,
                request.DisplayName,
                request.Description,
                request.Permissions?.ToList() ?? []);

            return Results.Created($"/api/clients/{clientId}/roles/{role.Id}", role);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = "invalid_permissions", error_description = ex.Message });
        }
    }

    private static async Task<IResult> UpdateRoleAsync(
        string clientId,
        string roleId,
        UpdateRoleRequest request,
        HttpContext context,
        IClusterClient clusterClient)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantId))
            return Results.Unauthorized();

        return await UpdateRoleInternalAsync(tenantId, clientId, roleId, request, context, clusterClient);
    }

    private static async Task<IResult> UpdateRoleWithTenantAsync(
        string tenantId,
        string clientId,
        string roleId,
        UpdateRoleRequest request,
        HttpContext context,
        IClusterClient clusterClient)
    {
        return await UpdateRoleInternalAsync(tenantId, clientId, roleId, request, context, clusterClient);
    }

    private static async Task<IResult> UpdateRoleInternalAsync(
        string tenantId,
        string clientId,
        string roleId,
        UpdateRoleRequest request,
        HttpContext context,
        IClusterClient clusterClient)
    {
        if (!await IsAdminAsync(context, clusterClient, tenantId))
            return Results.Forbid();

        var clientGrain = clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        if (!await clientGrain.ExistsAsync())
            return Results.NotFound(new { error = "client_not_found" });

        try
        {
            var role = await clientGrain.UpdateApplicationRoleAsync(
                roleId,
                request.Name,
                request.DisplayName,
                request.Description,
                request.Permissions?.ToList());

            if (role == null)
                return Results.NotFound(new { error = "role_not_found" });

            return Results.Ok(role);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = "invalid_permissions", error_description = ex.Message });
        }
    }

    private static async Task<IResult> DeleteRoleAsync(
        string clientId,
        string roleId,
        HttpContext context,
        IClusterClient clusterClient)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantId))
            return Results.Unauthorized();

        return await DeleteRoleInternalAsync(tenantId, clientId, roleId, context, clusterClient);
    }

    private static async Task<IResult> DeleteRoleWithTenantAsync(
        string tenantId,
        string clientId,
        string roleId,
        HttpContext context,
        IClusterClient clusterClient)
    {
        return await DeleteRoleInternalAsync(tenantId, clientId, roleId, context, clusterClient);
    }

    private static async Task<IResult> DeleteRoleInternalAsync(
        string tenantId,
        string clientId,
        string roleId,
        HttpContext context,
        IClusterClient clusterClient)
    {
        if (!await IsAdminAsync(context, clusterClient, tenantId))
            return Results.Forbid();

        var clientGrain = clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        if (!await clientGrain.ExistsAsync())
            return Results.NotFound(new { error = "client_not_found" });

        var deleted = await clientGrain.DeleteApplicationRoleAsync(roleId);
        if (!deleted)
            return Results.NotFound(new { error = "role_not_found_or_system" });

        return Results.NoContent();
    }

    #endregion

    #region User Role Assignment Endpoints

    private static async Task<IResult> GetUserRolesAsync(
        string clientId,
        string userId,
        HttpContext context,
        IClusterClient clusterClient)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantId))
            return Results.Unauthorized();

        return await GetUserRolesInternalAsync(tenantId, clientId, userId, context, clusterClient);
    }

    private static async Task<IResult> GetUserRolesWithTenantAsync(
        string tenantId,
        string clientId,
        string userId,
        HttpContext context,
        IClusterClient clusterClient)
    {
        return await GetUserRolesInternalAsync(tenantId, clientId, userId, context, clusterClient);
    }

    private static async Task<IResult> GetUserRolesInternalAsync(
        string tenantId,
        string clientId,
        string userId,
        HttpContext context,
        IClusterClient clusterClient)
    {
        if (!await IsAdminAsync(context, clusterClient, tenantId))
            return Results.Forbid();

        var clientGrain = clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        if (!await clientGrain.ExistsAsync())
            return Results.NotFound(new { error = "client_not_found" });

        var userAppRolesGrain = clusterClient.GetGrain<IUserApplicationRolesGrain>(
            $"{tenantId}/client-{clientId}/user-{userId}");

        var assignments = await userAppRolesGrain.GetRoleAssignmentsAsync();
        return Results.Ok(new UserApplicationRolesResponse { Assignments = assignments.ToList() });
    }

    private static async Task<IResult> AssignUserRoleAsync(
        string clientId,
        string userId,
        AssignRoleRequest request,
        HttpContext context,
        IClusterClient clusterClient)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantId))
            return Results.Unauthorized();

        return await AssignUserRoleInternalAsync(tenantId, clientId, userId, request, context, clusterClient);
    }

    private static async Task<IResult> AssignUserRoleWithTenantAsync(
        string tenantId,
        string clientId,
        string userId,
        AssignRoleRequest request,
        HttpContext context,
        IClusterClient clusterClient)
    {
        return await AssignUserRoleInternalAsync(tenantId, clientId, userId, request, context, clusterClient);
    }

    private static async Task<IResult> AssignUserRoleInternalAsync(
        string tenantId,
        string clientId,
        string userId,
        AssignRoleRequest request,
        HttpContext context,
        IClusterClient clusterClient)
    {
        if (!await IsAdminAsync(context, clusterClient, tenantId))
            return Results.Forbid();

        var clientGrain = clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        if (!await clientGrain.ExistsAsync())
            return Results.NotFound(new { error = "client_not_found" });

        // Validate role exists
        var role = await clientGrain.GetApplicationRoleAsync(request.RoleId);
        if (role == null)
            return Results.NotFound(new { error = "role_not_found" });

        // Validate organization-scoped assignment
        var scope = request.Scope ?? ApplicationRoleScope.Tenant;
        if (scope == ApplicationRoleScope.Organization && string.IsNullOrEmpty(request.OrganizationId))
        {
            return Results.BadRequest(new { error = "invalid_request", error_description = "OrganizationId is required for organization-scoped roles" });
        }

        var userAppRolesGrain = clusterClient.GetGrain<IUserApplicationRolesGrain>(
            $"{tenantId}/client-{clientId}/user-{userId}");

        await userAppRolesGrain.AssignRoleAsync(request.RoleId, scope, request.OrganizationId);
        return Results.Created($"/api/clients/{clientId}/users/{userId}/roles/{request.RoleId}", new { roleId = request.RoleId, scope, organizationId = request.OrganizationId });
    }

    private static async Task<IResult> RemoveUserRoleAsync(
        string clientId,
        string userId,
        string roleId,
        HttpContext context,
        IClusterClient clusterClient,
        string? organizationId = null)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantId))
            return Results.Unauthorized();

        return await RemoveUserRoleInternalAsync(tenantId, clientId, userId, roleId, organizationId, context, clusterClient);
    }

    private static async Task<IResult> RemoveUserRoleWithTenantAsync(
        string tenantId,
        string clientId,
        string userId,
        string roleId,
        HttpContext context,
        IClusterClient clusterClient,
        string? organizationId = null)
    {
        return await RemoveUserRoleInternalAsync(tenantId, clientId, userId, roleId, organizationId, context, clusterClient);
    }

    private static async Task<IResult> RemoveUserRoleInternalAsync(
        string tenantId,
        string clientId,
        string userId,
        string roleId,
        string? organizationId,
        HttpContext context,
        IClusterClient clusterClient)
    {
        if (!await IsAdminAsync(context, clusterClient, tenantId))
            return Results.Forbid();

        var clientGrain = clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        if (!await clientGrain.ExistsAsync())
            return Results.NotFound(new { error = "client_not_found" });

        var userAppRolesGrain = clusterClient.GetGrain<IUserApplicationRolesGrain>(
            $"{tenantId}/client-{clientId}/user-{userId}");

        await userAppRolesGrain.RemoveRoleAsync(roleId, organizationId);
        return Results.NoContent();
    }

    #endregion

    #region System Sync Endpoints (CCF)

    private static async Task<IResult> SyncSystemPermissionsAsync(
        string clientId,
        SyncPermissionsRequest request,
        HttpContext context,
        IClusterClient clusterClient)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantId))
            return Results.Unauthorized();

        // Validate caller is the application itself (CCF token)
        if (!IsCallerTheApplication(context, clientId))
            return Results.Forbid();

        var clientGrain = clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        if (!await clientGrain.ExistsAsync())
            return Results.NotFound(new { error = "client_not_found" });

        // Convert request permissions to ApplicationPermission with IsSystem = true
        var permissions = request.Permissions.Select(p => new ApplicationPermission
        {
            Name = p.Name,
            DisplayName = p.DisplayName,
            Description = p.Description,
            IsSystem = true
        }).ToList();

        var result = await clientGrain.SyncSystemPermissionsAsync(permissions);

        if (!result.Success)
            return Results.BadRequest(new { error = "sync_failed", error_description = result.Error });

        return Results.Ok(result);
    }

    private static async Task<IResult> SyncSystemRolesAsync(
        string clientId,
        SyncRolesRequest request,
        HttpContext context,
        IClusterClient clusterClient)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantId))
            return Results.Unauthorized();

        // Validate caller is the application itself (CCF token)
        if (!IsCallerTheApplication(context, clientId))
            return Results.Forbid();

        var clientGrain = clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        if (!await clientGrain.ExistsAsync())
            return Results.NotFound(new { error = "client_not_found" });

        // Convert request roles to ApplicationRole with IsSystem = true
        var roles = request.Roles.Select(r => new ApplicationRole
        {
            Id = r.Id ?? Guid.NewGuid().ToString("N")[..12],
            Name = r.Name,
            DisplayName = r.DisplayName,
            Description = r.Description,
            Permissions = r.Permissions?.ToList() ?? [],
            IsSystem = true,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        var result = await clientGrain.SyncSystemRolesAsync(roles);

        if (!result.Success)
            return Results.BadRequest(new { error = "sync_failed", error_description = result.Error });

        return Results.Ok(result);
    }

    /// <summary>
    /// Validates that the caller is the application itself via Client Credentials Flow.
    /// In CCF tokens, both 'sub' and 'client_id' claims equal the client ID.
    /// </summary>
    private static bool IsCallerTheApplication(HttpContext context, string clientId)
    {
        var sub = context.User.FindFirst("sub")?.Value;
        var tokenClientId = context.User.FindFirst("client_id")?.Value;

        // In CCF, sub == client_id and both should match the route clientId
        return !string.IsNullOrEmpty(sub) &&
               !string.IsNullOrEmpty(tokenClientId) &&
               sub == tokenClientId &&
               sub == clientId;
    }

    #endregion

    #region Helpers

    private static async Task<bool> IsAdminAsync(HttpContext context, IClusterClient clusterClient, string tenantId)
    {
        var userId = context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return false;

        var membershipGrain = clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{userId}");
        var isAdmin = await membershipGrain.HasRoleAsync(WellKnownRoles.TenantAdmin) ||
                      await membershipGrain.HasRoleAsync(WellKnownRoles.TenantOwner);

        return isAdmin;
    }

    #endregion
}

#region Request/Response Models

public sealed class AddPermissionRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class ApplicationPermissionsResponse
{
    [JsonPropertyName("permissions")]
    public List<ApplicationPermission> Permissions { get; set; } = [];
}

public sealed class CreateRoleRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("permissions")]
    public string[]? Permissions { get; set; }
}

public sealed class UpdateRoleRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("permissions")]
    public string[]? Permissions { get; set; }
}

public sealed class ApplicationRolesResponse
{
    [JsonPropertyName("roles")]
    public List<ApplicationRole> Roles { get; set; } = [];
}

public sealed class AssignRoleRequest
{
    [JsonPropertyName("role_id")]
    public string RoleId { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public ApplicationRoleScope? Scope { get; set; }

    [JsonPropertyName("organization_id")]
    public string? OrganizationId { get; set; }
}

public sealed class UserApplicationRolesResponse
{
    [JsonPropertyName("assignments")]
    public List<ApplicationRoleAssignment> Assignments { get; set; } = [];
}

public sealed class SyncPermissionsRequest
{
    [JsonPropertyName("permissions")]
    public List<SyncPermissionItem> Permissions { get; set; } = [];
}

public sealed class SyncPermissionItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class SyncRolesRequest
{
    [JsonPropertyName("roles")]
    public List<SyncRoleItem> Roles { get; set; } = [];
}

public sealed class SyncRoleItem
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("permissions")]
    public string[]? Permissions { get; set; }
}

#endregion
