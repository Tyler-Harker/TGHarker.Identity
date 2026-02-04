using TGHarker.Identity.Abstractions.Models;

namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Grain storing a user's application role assignments for a specific client.
/// Key: {tenantId}/client-{clientId}/user-{userId}
/// </summary>
public interface IUserApplicationRolesGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the current state of the user's application roles.
    /// </summary>
    Task<UserApplicationRolesState?> GetStateAsync();

    /// <summary>
    /// Checks if this grain has any state (user has any role assignments).
    /// </summary>
    Task<bool> ExistsAsync();

    /// <summary>
    /// Assigns a role to the user.
    /// </summary>
    /// <param name="roleId">The role ID to assign.</param>
    /// <param name="scope">Whether the role is tenant-wide or organization-specific.</param>
    /// <param name="organizationId">Required when scope is Organization.</param>
    Task AssignRoleAsync(string roleId, ApplicationRoleScope scope, string? organizationId = null);

    /// <summary>
    /// Removes a role from the user.
    /// </summary>
    /// <param name="roleId">The role ID to remove.</param>
    /// <param name="organizationId">If specified, only removes the organization-scoped assignment.</param>
    Task RemoveRoleAsync(string roleId, string? organizationId = null);

    /// <summary>
    /// Gets the effective role IDs for the user in a given context.
    /// Returns tenant-scoped roles plus any organization-scoped roles if organizationId is provided.
    /// </summary>
    Task<IReadOnlyList<string>> GetEffectiveRolesAsync(string? organizationId = null);

    /// <summary>
    /// Gets the effective permissions for the user in a given context.
    /// Resolves all role assignments to their permissions.
    /// </summary>
    Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(string? organizationId = null);

    /// <summary>
    /// Gets all role assignments for the user.
    /// </summary>
    Task<IReadOnlyList<ApplicationRoleAssignment>> GetRoleAssignmentsAsync();

    /// <summary>
    /// Clears all role assignments for the user.
    /// </summary>
    Task ClearAllRolesAsync();
}
