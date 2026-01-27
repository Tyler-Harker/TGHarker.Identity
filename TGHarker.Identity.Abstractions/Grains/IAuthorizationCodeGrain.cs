using TGHarker.Identity.Abstractions.Models;

namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Short-lived grain representing an OAuth authorization code.
/// Key: {tenantId}/code-{codeHash} (e.g., "acme/code-abc123")
/// Auto-deactivates after expiration.
/// </summary>
public interface IAuthorizationCodeGrain : IGrainWithStringKey
{
    Task<bool> CreateAsync(AuthorizationCodeState state);
    Task<AuthorizationCodeState?> RedeemAsync(string? codeVerifier);
    Task<bool> IsValidAsync();
    Task<AuthorizationCodeState?> GetStateAsync();
}
