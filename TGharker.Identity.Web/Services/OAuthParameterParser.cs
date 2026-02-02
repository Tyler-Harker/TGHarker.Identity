namespace TGharker.Identity.Web.Services;

/// <summary>
/// Parses OAuth2/OIDC parameters from query strings.
/// Thread-safe and can be registered as a singleton.
/// </summary>
public sealed class OAuthParameterParser : IOAuthParameterParser
{
    /// <inheritdoc />
    public OAuthParameters ParseFromQueryString(string queryString)
    {
        return OAuthParameters.FromQueryString(queryString);
    }

    /// <inheritdoc />
    public OAuthParameters? ParseFromReturnUrl(string? returnUrl)
    {
        return OAuthParameters.FromReturnUrl(returnUrl);
    }
}
