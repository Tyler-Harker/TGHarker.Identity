using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Grain representing a user's membership in a specific organization.
/// Stores organization-specific roles, claims, and settings.
/// Key: {tenantId}/org-{orgId}/member-{userId}
/// </summary>
public interface IOrganizationMembershipGrain : IGrainWithStringKey
{
    Task<OrganizationMembershipState?> GetStateAsync();
    Task<CreateOrganizationMembershipResult> CreateAsync(CreateOrganizationMembershipRequest request);
    Task<bool> IsActiveAsync();
    Task ActivateAsync();
    Task DeactivateAsync();
    Task<bool> ExistsAsync();

    // Organization-specific roles
    Task AddRoleAsync(string role);
    Task RemoveRoleAsync(string role);
    Task<IReadOnlyList<string>> GetRolesAsync();
    Task<bool> HasRoleAsync(string role);

    // Organization-specific claims
    Task AddClaimAsync(string type, string value);
    Task RemoveClaimAsync(string type);
    Task<IReadOnlyList<UserClaim>> GetClaimsAsync();
}
