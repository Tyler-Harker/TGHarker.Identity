using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Grain representing a user's membership in a specific tenant.
/// Stores tenant-specific roles, claims, and settings.
/// Key: {tenantId}/member-{userId} (e.g., "acme/member-abc123")
/// </summary>
public interface ITenantMembershipGrain : IGrainWithStringKey
{
    Task<TenantMembershipState?> GetStateAsync();
    Task<CreateMembershipResult> CreateAsync(CreateMembershipRequest request);
    Task UpdateAsync(UpdateMembershipRequest request);
    Task<bool> IsActiveAsync();
    Task ActivateAsync();
    Task DeactivateAsync();
    Task<bool> ExistsAsync();

    // Tenant-specific claims
    Task AddClaimAsync(string type, string value);
    Task RemoveClaimAsync(string type);
    Task<IReadOnlyList<UserClaim>> GetClaimsAsync();

    // Tenant-specific roles
    Task AddRoleAsync(string role);
    Task RemoveRoleAsync(string role);
    Task<IReadOnlyList<string>> GetRolesAsync();
    Task<bool> HasRoleAsync(string role);
}
