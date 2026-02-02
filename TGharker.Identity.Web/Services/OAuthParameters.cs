using System.Web;
using Microsoft.AspNetCore.Http;

namespace TGharker.Identity.Web.Services;

/// <summary>
/// DTO for OAuth2/OIDC request parameters.
/// Provides factory methods to parse parameters from various sources.
/// </summary>
public sealed record OAuthParameters
{
    public string? ClientId { get; init; }
    public string? Scope { get; init; }
    public string? RedirectUri { get; init; }
    public string? State { get; init; }
    public string? Nonce { get; init; }
    public string? CodeChallenge { get; init; }
    public string? CodeChallengeMethod { get; init; }
    public string? ResponseMode { get; init; }
    public string? OrganizationId { get; init; }

    /// <summary>
    /// Creates OAuthParameters from an IQueryCollection.
    /// ASP.NET Core automatically URL-decodes query parameters, so no additional decoding is needed.
    /// </summary>
    public static OAuthParameters FromQuery(IQueryCollection query)
    {
        return new OAuthParameters
        {
            ClientId = query["client_id"].FirstOrDefault(),
            Scope = query["scope"].FirstOrDefault(),
            RedirectUri = query["redirect_uri"].FirstOrDefault(),
            State = query["state"].FirstOrDefault(),
            Nonce = query["nonce"].FirstOrDefault(),
            CodeChallenge = query["code_challenge"].FirstOrDefault(),
            CodeChallengeMethod = query["code_challenge_method"].FirstOrDefault(),
            ResponseMode = query["response_mode"].FirstOrDefault(),
            OrganizationId = query["organization_id"].FirstOrDefault()
        };
    }

    /// <summary>
    /// Creates OAuthParameters from a query string.
    /// Uses HttpUtility.ParseQueryString which handles URL decoding correctly
    /// ('+' in query string represents space, '%2B' represents literal '+').
    /// </summary>
    public static OAuthParameters FromQueryString(string queryString)
    {
        // Remove leading '?' if present
        if (queryString.StartsWith('?'))
        {
            queryString = queryString[1..];
        }

        // HttpUtility.ParseQueryString handles URL decoding correctly:
        // - '+' in query string -> space (per application/x-www-form-urlencoded spec)
        // - '%2B' in query string -> literal '+' character
        var queryParams = HttpUtility.ParseQueryString(queryString);

        return new OAuthParameters
        {
            ClientId = queryParams["client_id"],
            Scope = queryParams["scope"],
            RedirectUri = queryParams["redirect_uri"],
            State = queryParams["state"],
            Nonce = queryParams["nonce"],
            CodeChallenge = queryParams["code_challenge"],
            CodeChallengeMethod = queryParams["code_challenge_method"],
            ResponseMode = queryParams["response_mode"],
            OrganizationId = queryParams["organization_id"]
        };
    }

    /// <summary>
    /// Creates OAuthParameters by extracting the query string from a return URL.
    /// Returns null if the URL has no query string or is invalid.
    /// </summary>
    public static OAuthParameters? FromReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl))
        {
            return null;
        }

        var queryIndex = returnUrl.IndexOf('?');
        if (queryIndex < 0)
        {
            return null;
        }

        var queryString = returnUrl[(queryIndex + 1)..];
        return FromQueryString(queryString);
    }
}
