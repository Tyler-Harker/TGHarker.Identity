namespace TGharker.Identity.Web.Services;

/// <summary>
/// Builds OAuth2/OIDC URLs with proper URL encoding.
/// Uses Uri.EscapeDataString (not HttpUtility.UrlEncode) to ensure
/// special characters like '+' are encoded as '%2B' (not '+').
/// </summary>
public interface IOAuthUrlBuilder
{
    /// <summary>
    /// Builds an authorize URL for redirecting to login, consent, etc.
    /// </summary>
    /// <param name="basePath">The base path (e.g., "/tenant/mytenant/login").</param>
    /// <param name="parameters">The OAuth parameters to include in the query string.</param>
    /// <returns>The fully constructed URL with properly encoded parameters.</returns>
    string BuildUrl(string basePath, OAuthParameters parameters);

    /// <summary>
    /// Builds an authorize URL with an additional returnUrl parameter.
    /// </summary>
    /// <param name="basePath">The base path (e.g., "/tenant/mytenant/login").</param>
    /// <param name="returnUrl">The return URL to include.</param>
    /// <returns>The fully constructed URL with properly encoded returnUrl.</returns>
    string BuildUrlWithReturnUrl(string basePath, string returnUrl);

    /// <summary>
    /// Builds an authorize URL combining OAuth parameters and a return URL.
    /// </summary>
    string BuildUrl(string basePath, OAuthParameters parameters, string? returnUrl);
}
