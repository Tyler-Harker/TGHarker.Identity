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
    Task<bool> IsActiveAsync();
    Task DeactivateAsync();
    Task ActivateAsync();
    Task<IReadOnlyList<string>> GetMemberUserIdsAsync();
    Task AddMemberAsync(string userId);
    Task RemoveMemberAsync(string userId);
    Task<bool> ExistsAsync();
}
