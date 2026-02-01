using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Orleans.Search.Abstractions.Attributes;

namespace TGHarker.Identity.Abstractions.Models;

[GenerateSerializer]
[Searchable(typeof(ITenantMembershipGrain))]
public sealed class TenantMembershipState
{
    [Id(0)]
    [Queryable(Indexed = true)]
    public string UserId { get; set; } = string.Empty;

    [Id(1)]
    [Queryable(Indexed = true)]
    public string TenantId { get; set; } = string.Empty;

    [Id(2)] public string? Username { get; set; }

    [Id(3)]
    [Queryable]
    public bool IsActive { get; set; }
    [Id(4)] public List<string> Roles { get; set; } = [];
    [Id(5)] public List<UserClaim> Claims { get; set; } = [];
    [Id(6)] public DateTime JoinedAt { get; set; }
    [Id(7)] public DateTime? LastAccessAt { get; set; }
    [Id(8)] public string? DefaultOrganizationId { get; set; }
}

[GenerateSerializer]
public sealed class UserClaim
{
    [Id(0)] public string Type { get; set; } = string.Empty;
    [Id(1)] public string Value { get; set; } = string.Empty;
}

public static class WellKnownRoles
{
    public const string TenantAdmin = "tenant_admin";
    public const string TenantOwner = "tenant_owner";
    public const string User = "user";
}

[GenerateSerializer]
public sealed class TenantRole
{
    [Id(0)] public string Id { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public string? Description { get; set; }
    [Id(3)] public bool IsSystem { get; set; }
    [Id(4)] public List<string> Permissions { get; set; } = [];
    [Id(5)] public DateTime CreatedAt { get; set; }
}

public static class WellKnownPermissions
{
    // Tenant management
    public const string TenantManage = "tenant:manage";
    public const string TenantViewSettings = "tenant:view_settings";

    // User management
    public const string UsersView = "users:view";
    public const string UsersInvite = "users:invite";
    public const string UsersRemove = "users:remove";
    public const string UsersManageRoles = "users:manage_roles";

    // Role management
    public const string RolesView = "roles:view";
    public const string RolesCreate = "roles:create";
    public const string RolesEdit = "roles:edit";
    public const string RolesDelete = "roles:delete";

    // Application/Client management
    public const string ClientsView = "clients:view";
    public const string ClientsCreate = "clients:create";
    public const string ClientsEdit = "clients:edit";
    public const string ClientsDelete = "clients:delete";
    public const string ClientsManageSecrets = "clients:manage_secrets";

    // Organization management
    public const string OrganizationsView = "organizations:view";
    public const string OrganizationsCreate = "organizations:create";
    public const string OrganizationsEdit = "organizations:edit";
    public const string OrganizationsDelete = "organizations:delete";
    public const string OrganizationsManageMembers = "organizations:manage_members";

    public static readonly IReadOnlyList<(string Permission, string Category, string Description)> All =
    [
        (TenantManage, "Tenant", "Manage tenant settings"),
        (TenantViewSettings, "Tenant", "View tenant settings"),
        (UsersView, "Users", "View tenant members"),
        (UsersInvite, "Users", "Invite users to tenant"),
        (UsersRemove, "Users", "Remove users from tenant"),
        (UsersManageRoles, "Users", "Assign and remove user roles"),
        (RolesView, "Roles", "View roles"),
        (RolesCreate, "Roles", "Create new roles"),
        (RolesEdit, "Roles", "Edit existing roles"),
        (RolesDelete, "Roles", "Delete roles"),
        (ClientsView, "Applications", "View applications"),
        (ClientsCreate, "Applications", "Create applications"),
        (ClientsEdit, "Applications", "Edit applications"),
        (ClientsDelete, "Applications", "Delete applications"),
        (ClientsManageSecrets, "Applications", "Manage client secrets"),
        (OrganizationsView, "Organizations", "View organizations"),
        (OrganizationsCreate, "Organizations", "Create organizations"),
        (OrganizationsEdit, "Organizations", "Edit organizations"),
        (OrganizationsDelete, "Organizations", "Delete organizations"),
        (OrganizationsManageMembers, "Organizations", "Manage organization members"),
    ];
}
