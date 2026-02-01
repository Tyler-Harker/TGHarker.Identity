using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using TGHarker.Identity.Abstractions.Grains;

namespace TGharker.Identity.Web.Services;

/// <summary>
/// Resolves signing keys for JWT validation by loading them from tenant-specific Orleans grains.
/// This allows the identity server to validate its own tokens in a multi-tenant environment.
/// </summary>
public interface ITenantSigningKeyResolver
{
    Task<IEnumerable<SecurityKey>> ResolveSigningKeysAsync(string tenantId);
}

public sealed class TenantSigningKeyResolver : ITenantSigningKeyResolver
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<TenantSigningKeyResolver> _logger;

    public TenantSigningKeyResolver(IClusterClient clusterClient, ILogger<TenantSigningKeyResolver> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public async Task<IEnumerable<SecurityKey>> ResolveSigningKeysAsync(string tenantId)
    {
        try
        {
            var signingKeyGrain = _clusterClient.GetGrain<ISigningKeyGrain>($"{tenantId}/signing-keys");
            var publicKeys = await signingKeyGrain.GetPublicKeysAsync();

            var securityKeys = new List<SecurityKey>();

            foreach (var key in publicKeys)
            {
                try
                {
                    var rsa = RSA.Create();
                    rsa.ImportFromPem(key.PublicKeyPem);

                    var rsaKey = new RsaSecurityKey(rsa)
                    {
                        KeyId = key.KeyId
                    };

                    securityKeys.Add(rsaKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load signing key {KeyId} for tenant {TenantId}", key.KeyId, tenantId);
                }
            }

            _logger.LogDebug("Resolved {KeyCount} signing keys for tenant {TenantId}", securityKeys.Count, tenantId);
            return securityKeys;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve signing keys for tenant {TenantId}", tenantId);
            return [];
        }
    }
}
