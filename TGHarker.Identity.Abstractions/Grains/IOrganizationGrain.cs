using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Grain representing an organization within a tenant.
/// Key: {tenantId}/org-{orgId}
/// </summary>
public interface IOrganizationGrain : IGrainWithStringKey
{
    Task<OrganizationState?> GetStateAsync();
    Task<CreateOrganizationResult> InitializeAsync(CreateOrganizationRequest request);
    Task UpdateAsync(UpdateOrganizationRequest request);
    Task<bool> IsActiveAsync();
    Task DeactivateAsync();
    Task ActivateAsync();
    Task<bool> ExistsAsync();

    // Member management
    Task<IReadOnlyList<string>> GetMemberUserIdsAsync();
    Task AddMemberAsync(string userId);
    Task RemoveMemberAsync(string userId);

    // Role management
    Task<IReadOnlyList<OrganizationRole>> GetRolesAsync();
    Task<OrganizationRole?> GetRoleAsync(string roleId);
    Task<OrganizationRole> CreateRoleAsync(string name, string? description, List<string> permissions);
    Task<OrganizationRole?> UpdateRoleAsync(string roleId, string name, string? description, List<string> permissions);
    Task<bool> DeleteRoleAsync(string roleId);
}
