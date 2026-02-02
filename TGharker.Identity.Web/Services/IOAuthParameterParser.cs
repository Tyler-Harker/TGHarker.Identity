namespace TGharker.Identity.Web.Services;

/// <summary>
/// Parses OAuth2/OIDC parameters from query strings.
/// Correctly handles URL encoding ('+' as space per spec, '%2B' as literal '+').
/// </summary>
public interface IOAuthParameterParser
{
    /// <summary>
    /// Parses OAuth parameters from a query string.
    /// Handles URL decoding correctly: '+' becomes space, '%2B' becomes '+'.
    /// </summary>
    /// <param name="queryString">The query string (with or without leading '?').</param>
    OAuthParameters ParseFromQueryString(string queryString);

    /// <summary>
    /// Parses OAuth parameters from a return URL by extracting its query string.
    /// Returns null if the URL has no query string.
    /// </summary>
    /// <param name="returnUrl">The return URL containing OAuth parameters.</param>
    OAuthParameters? ParseFromReturnUrl(string? returnUrl);
}
