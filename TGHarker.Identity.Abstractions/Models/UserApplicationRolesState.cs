using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Orleans.Search.Abstractions.Attributes;

namespace TGHarker.Identity.Abstractions.Models;

/// <summary>
/// Stores a user's application role assignments for a specific client application.
/// Grain key: {tenantId}/client-{clientId}/user-{userId}
/// </summary>
[GenerateSerializer]
[Alias("UserApplicationRolesState")]
[Searchable(typeof(IUserApplicationRolesGrain))]
public sealed class UserApplicationRolesState
{
    /// <summary>
    /// The user's ID.
    /// </summary>
    [Id(0)]
    [Queryable(Indexed = true)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The tenant this state belongs to.
    /// </summary>
    [Id(1)]
    [Queryable(Indexed = true)]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// The client application this state is scoped to.
    /// </summary>
    [Id(2)]
    [Queryable(Indexed = true)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The user's role assignments for this application.
    /// </summary>
    [Id(3)] public List<ApplicationRoleAssignment> RoleAssignments { get; set; } = [];
}
