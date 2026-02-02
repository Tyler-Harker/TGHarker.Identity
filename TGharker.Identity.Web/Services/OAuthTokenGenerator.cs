using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TGharker.Identity.Web.Services;

/// <summary>
/// Generates and hashes OAuth2/OIDC tokens using cryptographically secure methods.
/// Thread-safe and can be registered as a singleton.
/// </summary>
public sealed class OAuthTokenGenerator : IOAuthTokenGenerator
{
    private const int TokenSizeBytes = 32;

    /// <inheritdoc />
    public string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenSizeBytes);
        return Base64UrlEncoder.Encode(bytes);
    }

    /// <inheritdoc />
    public string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Base64UrlEncoder.Encode(hash);
    }
}
