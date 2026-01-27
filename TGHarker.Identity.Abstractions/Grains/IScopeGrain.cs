using TGHarker.Identity.Abstractions.Models;

namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Grain representing an OAuth/OIDC scope within a tenant.
/// Key: {tenantId}/scope-{scopeName} (e.g., "acme/scope-openid")
/// </summary>
public interface IScopeGrain : IGrainWithStringKey
{
    Task<ScopeState?> GetStateAsync();
    Task<bool> CreateAsync(ScopeState state);
    Task UpdateAsync(ScopeState state);
    Task<bool> ExistsAsync();
    Task<IReadOnlyList<ScopeClaim>> GetClaimsAsync();
}
