using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.Playwright;

namespace TGHarker.Identity.IntegrationTests.Infrastructure;

/// <summary>
/// Helper class to build and parse OIDC authorization requests/responses
/// </summary>
public class TestOidcClient
{
    private readonly string _clientId;
    private readonly string _redirectUri;
    private readonly string _baseUrl;
    private readonly string _tenantIdentifier;

    public TestOidcClient(string baseUrl, string tenantIdentifier, string clientId, string redirectUri)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _tenantIdentifier = tenantIdentifier;
        _clientId = clientId;
        _redirectUri = redirectUri;
    }

    public string AuthorizeEndpoint => $"{_baseUrl}/tenant/{_tenantIdentifier}/connect/authorize";
    public string TokenEndpoint => $"{_baseUrl}/tenant/{_tenantIdentifier}/connect/token";

    /// <summary>
    /// Generates a PKCE code verifier
    /// </summary>
    public static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Creates a PKCE code challenge from a verifier
    /// </summary>
    public static string CreateCodeChallenge(string verifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Builds an authorization URL with all required parameters
    /// </summary>
    public AuthorizationRequest BuildAuthorizationRequest(
        string scope = "openid profile email",
        string? state = null,
        string? nonce = null,
        string? codeVerifier = null)
    {
        state ??= Guid.NewGuid().ToString("N");
        nonce ??= Guid.NewGuid().ToString("N");
        codeVerifier ??= GenerateCodeVerifier();
        var codeChallenge = CreateCodeChallenge(codeVerifier);

        var queryParams = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = _clientId,
            ["redirect_uri"] = _redirectUri,
            ["scope"] = scope,
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["response_mode"] = "query"
        };

        var queryString = string.Join("&", queryParams.Select(kvp =>
            $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));

        return new AuthorizationRequest
        {
            Url = $"{AuthorizeEndpoint}?{queryString}",
            State = state,
            Nonce = nonce,
            CodeVerifier = codeVerifier,
            CodeChallenge = codeChallenge
        };
    }

    /// <summary>
    /// Parses the authorization response from a redirect URL
    /// </summary>
    public static AuthorizationResponse ParseAuthorizationResponse(string url)
    {
        var uri = new Uri(url);
        var query = HttpUtility.ParseQueryString(uri.Query);

        return new AuthorizationResponse
        {
            Code = query["code"],
            State = query["state"],
            Error = query["error"],
            ErrorDescription = query["error_description"]
        };
    }

    /// <summary>
    /// Checks if a URL is a redirect to the configured redirect_uri
    /// </summary>
    public bool IsRedirectToClient(string url)
    {
        return url.StartsWith(_redirectUri, StringComparison.OrdinalIgnoreCase);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

public class AuthorizationRequest
{
    public required string Url { get; init; }
    public required string State { get; init; }
    public required string Nonce { get; init; }
    public required string CodeVerifier { get; init; }
    public required string CodeChallenge { get; init; }
}

public class AuthorizationResponse
{
    public string? Code { get; init; }
    public string? State { get; init; }
    public string? Error { get; init; }
    public string? ErrorDescription { get; init; }

    public bool IsSuccess => !string.IsNullOrEmpty(Code) && string.IsNullOrEmpty(Error);
}
