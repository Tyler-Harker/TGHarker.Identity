namespace TGharker.Identity.Web.Services;

/// <summary>
/// Builds OAuth2/OIDC responses (redirects, form_post, errors).
/// </summary>
public interface IOAuthResponseBuilder
{
    /// <summary>
    /// Creates a success redirect response with the authorization code.
    /// </summary>
    /// <param name="redirectUri">The client's redirect URI.</param>
    /// <param name="code">The authorization code.</param>
    /// <param name="state">The state parameter (if provided by client).</param>
    /// <param name="responseMode">The response mode ("query", "fragment", or "form_post").</param>
    IResult CreateSuccessRedirect(string redirectUri, string code, string? state, string responseMode);

    /// <summary>
    /// Creates a success redirect response with custom parameters.
    /// </summary>
    /// <param name="redirectUri">The client's redirect URI.</param>
    /// <param name="parameters">The parameters to include in the response.</param>
    /// <param name="responseMode">The response mode ("query", "fragment", or "form_post").</param>
    IResult CreateSuccessRedirect(string redirectUri, Dictionary<string, string?> parameters, string responseMode);

    /// <summary>
    /// Creates an error redirect response.
    /// If redirectUri is null or empty, returns a BadRequest with JSON error.
    /// </summary>
    /// <param name="redirectUri">The client's redirect URI (may be null for pre-validation errors).</param>
    /// <param name="state">The state parameter (if provided by client).</param>
    /// <param name="responseMode">The response mode ("query", "fragment", or "form_post").</param>
    /// <param name="error">The OAuth error code (e.g., "invalid_request").</param>
    /// <param name="description">Optional error description.</param>
    IResult CreateErrorRedirect(string? redirectUri, string? state, string responseMode, string error, string? description);

    /// <summary>
    /// Creates a form_post response that auto-submits to the redirect URI.
    /// </summary>
    /// <param name="redirectUri">The client's redirect URI.</param>
    /// <param name="parameters">The parameters to include as hidden form fields.</param>
    IResult CreateFormPostResponse(string redirectUri, Dictionary<string, string?> parameters);
}
