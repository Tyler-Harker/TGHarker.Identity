using TGHarker.Identity.Abstractions.Models;

namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Grain representing an OAuth refresh token.
/// Key: {tenantId}/rt-{tokenHash} (e.g., "acme/rt-abc123")
/// </summary>
public interface IRefreshTokenGrain : IGrainWithStringKey
{
    Task<bool> CreateAsync(RefreshTokenState state);
    Task<RefreshTokenState?> ValidateAndRevokeAsync();
    Task RevokeAsync();
    Task<bool> IsValidAsync();
    Task<RefreshTokenState?> GetStateAsync();

    /// <summary>
    /// Sets the hash of the token that replaced this one during rotation.
    /// Used for token reuse detection - if someone tries to use a rotated token,
    /// the entire token chain can be revoked to protect against stolen tokens.
    /// </summary>
    Task SetReplacementTokenAsync(string replacementTokenHash);
}
