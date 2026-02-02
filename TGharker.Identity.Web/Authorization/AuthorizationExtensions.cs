using Microsoft.AspNetCore.Authorization;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Authorization;

public static class AuthorizationExtensions
{
    /// <summary>
    /// Adds permission-based authorization policies.
    /// Creates a policy for each permission in WellKnownPermissions.
    /// </summary>
    public static AuthorizationOptions AddPermissionPolicies(this AuthorizationOptions options)
    {
        // Create a policy for each well-known permission
        foreach (var (permission, category, description) in WellKnownPermissions.All)
        {
            // Policy name matches the permission name (e.g., "users:view")
            options.AddPolicy(permission, policy =>
                policy.Requirements.Add(new PermissionRequirement(permission)));
        }

        // Add combined policies for common use cases
        options.AddPolicy("CanManageUsers", policy =>
            policy.Requirements.Add(new AnyPermissionRequirement(
                WellKnownPermissions.UsersInvite,
                WellKnownPermissions.UsersRemove,
                WellKnownPermissions.UsersManageRoles)));

        options.AddPolicy("CanManageClients", policy =>
            policy.Requirements.Add(new AnyPermissionRequirement(
                WellKnownPermissions.ClientsCreate,
                WellKnownPermissions.ClientsEdit,
                WellKnownPermissions.ClientsDelete)));

        options.AddPolicy("CanManageRoles", policy =>
            policy.Requirements.Add(new AnyPermissionRequirement(
                WellKnownPermissions.RolesCreate,
                WellKnownPermissions.RolesEdit,
                WellKnownPermissions.RolesDelete)));

        options.AddPolicy("CanManageOrganizations", policy =>
            policy.Requirements.Add(new AnyPermissionRequirement(
                WellKnownPermissions.OrganizationsCreate,
                WellKnownPermissions.OrganizationsEdit,
                WellKnownPermissions.OrganizationsDelete,
                WellKnownPermissions.OrganizationsManageMembers)));

        options.AddPolicy("CanManageTenant", policy =>
            policy.Requirements.Add(new PermissionRequirement(WellKnownPermissions.TenantManage)));

        return options;
    }
}
