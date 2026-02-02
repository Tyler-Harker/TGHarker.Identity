namespace TGharker.Identity.Web.Services;

/// <summary>
/// Generates and hashes OAuth2/OIDC tokens (authorization codes, refresh tokens).
/// Uses cryptographically secure random generation and SHA256 hashing.
/// </summary>
public interface IOAuthTokenGenerator
{
    /// <summary>
    /// Generates a cryptographically secure random token.
    /// Returns 32 random bytes encoded as Base64URL (no padding).
    /// </summary>
    string GenerateToken();

    /// <summary>
    /// Computes a SHA256 hash of the token for secure storage.
    /// Returns the hash encoded as Base64URL (no padding).
    /// </summary>
    /// <param name="token">The token to hash.</param>
    string HashToken(string token);
}
