using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Grain representing a tenant in the identity system.
/// Key: {tenantIdentifier} (e.g., "acme")
/// </summary>
public interface ITenantGrain : IGrainWithStringKey
{
    Task<TenantState?> GetStateAsync();
    Task<CreateTenantResult> InitializeAsync(CreateTenantRequest request);
    Task UpdateConfigurationAsync(TenantConfiguration config);
    Task UpdateBrandingAsync(TenantBranding branding);
    Task<bool> IsActiveAsync();
    Task DeactivateAsync();
    Task ActivateAsync();
    Task<IReadOnlyList<string>> GetMemberUserIdsAsync();
    Task AddMemberAsync(string userId);
    Task RemoveMemberAsync(string userId);
    Task<bool> ExistsAsync();

    // Client/Application management
    Task<IReadOnlyList<string>> GetClientIdsAsync();
    Task AddClientAsync(string clientId);
    Task RemoveClientAsync(string clientId);

    // Role management
    Task<IReadOnlyList<TenantRole>> GetRolesAsync();
    Task<TenantRole?> GetRoleAsync(string roleId);
    Task<TenantRole> CreateRoleAsync(string name, string? description, List<string> permissions);
    Task<TenantRole?> UpdateRoleAsync(string roleId, string name, string? description, List<string> permissions);
    Task<bool> DeleteRoleAsync(string roleId);

    // Organization management
    Task<IReadOnlyList<string>> GetOrganizationIdsAsync();
    Task AddOrganizationAsync(string organizationId);
    Task RemoveOrganizationAsync(string organizationId);
}
