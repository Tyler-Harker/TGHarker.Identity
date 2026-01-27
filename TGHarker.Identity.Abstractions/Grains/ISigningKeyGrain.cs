using TGHarker.Identity.Abstractions.Models;

namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Grain managing signing keys for a tenant.
/// Key: {tenantId}/signing-keys (e.g., "acme/signing-keys")
/// </summary>
public interface ISigningKeyGrain : IGrainWithStringKey
{
    Task<SigningKeyState?> GetStateAsync();
    Task<SigningKey?> GetActiveKeyAsync();
    Task<IReadOnlyList<SigningKey>> GetPublicKeysAsync();
    Task<string> GenerateNewKeyAsync();
    Task RotateKeyAsync();
    Task RevokeKeyAsync(string keyId);
}
