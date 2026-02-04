using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Orleans.Search.Abstractions.Attributes;

namespace TGHarker.Identity.Abstractions.Models;

/// <summary>
/// Represents a searchable role assignment for a user within an application.
/// Grain key: {tenantId}/client-{clientId}/user-{userId}/role-{roleId}[/org-{orgId}]
/// </summary>
[GenerateSerializer]
[Alias("ApplicationRoleAssignmentState")]
[Searchable(typeof(IApplicationRoleAssignmentGrain))]
public sealed class ApplicationRoleAssignmentState
{
    /// <summary>
    /// Unique identifier for this assignment.
    /// </summary>
    [Id(0)] public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The user this role is assigned to.
    /// </summary>
    [Id(1)]
    [Queryable(Indexed = true)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The tenant this assignment belongs to.
    /// </summary>
    [Id(2)]
    [Queryable(Indexed = true)]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// The client application this assignment is for.
    /// </summary>
    [Id(3)]
    [Queryable(Indexed = true)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The application ID (alias for ClientId, for semantic clarity in queries).
    /// </summary>
    [Id(10)]
    [Queryable(Indexed = true)]
    public string ApplicationId { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the assigned role.
    /// </summary>
    [Id(4)]
    [Queryable(Indexed = true)]
    public string RoleId { get; set; } = string.Empty;

    /// <summary>
    /// The scope of this role assignment (tenant-wide or organization-specific).
    /// </summary>
    [Id(5)]
    [Queryable]
    public ApplicationRoleScope Scope { get; set; }

    /// <summary>
    /// The organization ID when Scope is Organization. Null for tenant-scoped assignments.
    /// </summary>
    [Id(6)]
    [Queryable(Indexed = true)]
    public string? OrganizationId { get; set; }

    /// <summary>
    /// When this role was assigned.
    /// </summary>
    [Id(7)] public DateTime AssignedAt { get; set; }

    /// <summary>
    /// Who assigned this role (user ID or "system").
    /// </summary>
    [Id(8)] public string? AssignedBy { get; set; }

    /// <summary>
    /// Whether this assignment is currently active.
    /// </summary>
    [Id(9)]
    [Queryable]
    public bool IsActive { get; set; } = true;
}
