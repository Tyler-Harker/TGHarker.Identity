using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;

namespace TGharker.Identity.Web.Services;

public sealed partial class PkceValidator : IPkceValidator
{
    public bool Validate(string codeVerifier, string codeChallenge, string? codeChallengeMethod)
    {
        if (string.IsNullOrEmpty(codeVerifier) || string.IsNullOrEmpty(codeChallenge))
            return false;

        // Validate code_verifier format: 43-128 characters, [A-Z] / [a-z] / [0-9] / "-" / "." / "_" / "~"
        if (codeVerifier.Length < 43 || codeVerifier.Length > 128)
            return false;

        if (!CodeVerifierRegex().IsMatch(codeVerifier))
            return false;

        var computedChallenge = GenerateCodeChallenge(codeVerifier, codeChallengeMethod ?? "S256");

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedChallenge),
            Encoding.UTF8.GetBytes(codeChallenge));
    }

    public string GenerateCodeChallenge(string codeVerifier, string method = "S256")
    {
        if (method == "S256")
        {
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
            return Base64UrlEncoder.Encode(hash);
        }

        if (method == "plain")
        {
            return codeVerifier;
        }

        throw new ArgumentException($"Unsupported code challenge method: {method}");
    }

    [GeneratedRegex(@"^[A-Za-z0-9\-._~]+$")]
    private static partial Regex CodeVerifierRegex();
}
