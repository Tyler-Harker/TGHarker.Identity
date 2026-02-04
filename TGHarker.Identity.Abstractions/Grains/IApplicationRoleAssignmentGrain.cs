using TGHarker.Identity.Abstractions.Models;

namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Grain representing a single application role assignment.
/// Key: {tenantId}/client-{clientId}/assignment-{assignmentId}
/// </summary>
public interface IApplicationRoleAssignmentGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the current state of this assignment.
    /// </summary>
    Task<ApplicationRoleAssignmentState?> GetStateAsync();

    /// <summary>
    /// Returns true if this assignment exists.
    /// </summary>
    Task<bool> ExistsAsync();

    /// <summary>
    /// Creates a new role assignment.
    /// </summary>
    Task<bool> CreateAsync(ApplicationRoleAssignmentState state);

    /// <summary>
    /// Deactivates this role assignment.
    /// </summary>
    Task<bool> DeactivateAsync();

    /// <summary>
    /// Reactivates this role assignment.
    /// </summary>
    Task<bool> ReactivateAsync();

    /// <summary>
    /// Deletes this role assignment permanently.
    /// </summary>
    Task<bool> DeleteAsync();
}
