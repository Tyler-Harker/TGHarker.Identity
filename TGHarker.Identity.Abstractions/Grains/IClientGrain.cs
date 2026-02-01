using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Grain representing an OAuth client/application within a tenant.
/// Key: {tenantId}/{clientId} (e.g., "acme/my-spa-app")
/// </summary>
public interface IClientGrain : IGrainWithStringKey
{
    Task<ClientState?> GetStateAsync();
    Task<CreateClientResult> CreateAsync(CreateClientRequest request);
    Task<string?> UpdateAsync(UpdateClientRequest request);
    Task<bool> ValidateSecretAsync(string secret);
    Task<AddSecretResult> AddSecretAsync(string description, DateTime? expiresAt);
    Task<bool> RevokeSecretAsync(string secretId);
    Task<bool> ValidateRedirectUriAsync(string redirectUri);
    Task<bool> ValidateScopeAsync(string scope);
    Task<bool> ValidateGrantTypeAsync(string grantType);
    Task<bool> ValidatePostLogoutRedirectUriAsync(string uri);
    Task<bool> IsActiveAsync();
    Task ActivateAsync();
    Task DeactivateAsync();
    Task<bool> ExistsAsync();
    Task<UserFlowSettings> GetUserFlowSettingsAsync();
}
