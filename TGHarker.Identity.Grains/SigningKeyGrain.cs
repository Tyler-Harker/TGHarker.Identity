using Orleans.Runtime;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using System.Security.Cryptography;

namespace TGHarker.Identity.Grains;

public sealed class SigningKeyGrain : Grain, ISigningKeyGrain
{
    private readonly IPersistentState<SigningKeyState> _state;

    public SigningKeyGrain(
        [PersistentState("signingKeys", "Default")] IPersistentState<SigningKeyState> state)
    {
        _state = state;
    }

    public Task<SigningKeyState?> GetStateAsync()
    {
        if (string.IsNullOrEmpty(_state.State.TenantId) && _state.State.Keys.Count == 0)
            return Task.FromResult<SigningKeyState?>(null);

        return Task.FromResult<SigningKeyState?>(_state.State);
    }

    public Task<SigningKey?> GetActiveKeyAsync()
    {
        var activeKey = _state.State.Keys
            .Where(k => k.IsActive && (k.ExpiresAt == null || k.ExpiresAt > DateTime.UtcNow) && k.RevokedAt == null)
            .OrderByDescending(k => k.CreatedAt)
            .FirstOrDefault();

        return Task.FromResult(activeKey);
    }

    public Task<IReadOnlyList<SigningKey>> GetPublicKeysAsync()
    {
        // Return all non-revoked keys for JWKS (allows validation of tokens signed with older keys)
        var publicKeys = _state.State.Keys
            .Where(k => k.RevokedAt == null && (k.ExpiresAt == null || k.ExpiresAt > DateTime.UtcNow))
            .Select(k => new SigningKey
            {
                KeyId = k.KeyId,
                Algorithm = k.Algorithm,
                PublicKeyPem = k.PublicKeyPem,
                IsActive = k.IsActive,
                CreatedAt = k.CreatedAt,
                ExpiresAt = k.ExpiresAt
                // PrivateKeyPem is intentionally not included
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<SigningKey>>(publicKeys);
    }

    public async Task<string> GenerateNewKeyAsync()
    {
        // Extract tenant ID from grain key
        var grainKey = this.GetPrimaryKeyString();
        var tenantId = grainKey.Contains('/') ? grainKey.Split('/')[0] : grainKey;

        _state.State.TenantId = tenantId;

        using var rsa = RSA.Create(2048);

        var privateKeyPem = rsa.ExportRSAPrivateKeyPem();
        var publicKeyPem = rsa.ExportRSAPublicKeyPem();

        var keyId = Guid.NewGuid().ToString("N")[..16];

        var newKey = new SigningKey
        {
            KeyId = keyId,
            Algorithm = "RS256",
            PrivateKeyPem = privateKeyPem,
            PublicKeyPem = publicKeyPem,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddYears(1)
        };

        _state.State.Keys.Add(newKey);
        await _state.WriteStateAsync();

        return keyId;
    }

    public async Task RotateKeyAsync()
    {
        // Deactivate all current active keys
        foreach (var key in _state.State.Keys.Where(k => k.IsActive))
        {
            key.IsActive = false;
        }

        // Generate a new active key
        await GenerateNewKeyAsync();
    }

    public async Task RevokeKeyAsync(string keyId)
    {
        var key = _state.State.Keys.FirstOrDefault(k => k.KeyId == keyId);

        if (key != null)
        {
            key.RevokedAt = DateTime.UtcNow;
            key.IsActive = false;
            await _state.WriteStateAsync();
        }
    }
}
