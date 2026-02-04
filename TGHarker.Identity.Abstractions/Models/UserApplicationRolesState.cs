namespace TGHarker.Identity.Abstractions.Models;

/// <summary>
/// Stores a user's application role assignments for a specific client application.
/// Grain key: {tenantId}/client-{clientId}/user-{userId}
/// </summary>
[GenerateSerializer]
public sealed class UserApplicationRolesState
{
    /// <summary>
    /// The user's ID.
    /// </summary>
    [Id(0)] public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The tenant this state belongs to.
    /// </summary>
    [Id(1)] public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// The client application this state is scoped to.
    /// </summary>
    [Id(2)] public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The user's role assignments for this application.
    /// </summary>
    [Id(3)] public List<ApplicationRoleAssignment> RoleAssignments { get; set; } = [];
}
