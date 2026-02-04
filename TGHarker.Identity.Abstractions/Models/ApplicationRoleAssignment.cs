namespace TGHarker.Identity.Abstractions.Models;

/// <summary>
/// Represents a role assignment for a user within an application,
/// with optional organization scoping.
/// </summary>
[GenerateSerializer]
public sealed class ApplicationRoleAssignment
{
    /// <summary>
    /// The ID of the assigned role.
    /// </summary>
    [Id(0)] public string RoleId { get; set; } = string.Empty;

    /// <summary>
    /// The scope of this role assignment (tenant-wide or organization-specific).
    /// </summary>
    [Id(1)] public ApplicationRoleScope Scope { get; set; }

    /// <summary>
    /// The organization ID when Scope is Organization. Null for tenant-scoped assignments.
    /// </summary>
    [Id(2)] public string? OrganizationId { get; set; }

    /// <summary>
    /// When this role was assigned.
    /// </summary>
    [Id(3)] public DateTime AssignedAt { get; set; }
}

/// <summary>
/// Defines the scope of an application role assignment.
/// </summary>
public enum ApplicationRoleScope
{
    /// <summary>
    /// Role applies across all organizations within the tenant.
    /// </summary>
    Tenant = 0,

    /// <summary>
    /// Role applies only within a specific organization.
    /// </summary>
    Organization = 1
}
