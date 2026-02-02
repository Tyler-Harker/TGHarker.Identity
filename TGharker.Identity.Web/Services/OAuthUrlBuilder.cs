using System.Text;

namespace TGharker.Identity.Web.Services;

/// <summary>
/// Builds OAuth2/OIDC URLs with proper URL encoding using Uri.EscapeDataString.
/// Thread-safe and can be registered as a singleton.
/// </summary>
public sealed class OAuthUrlBuilder : IOAuthUrlBuilder
{
    /// <inheritdoc />
    public string BuildUrl(string basePath, OAuthParameters parameters)
    {
        var sb = new StringBuilder(basePath);
        sb.Append('?');
        AppendOAuthParameters(sb, parameters);
        return sb.ToString();
    }

    /// <inheritdoc />
    public string BuildUrlWithReturnUrl(string basePath, string returnUrl)
    {
        return $"{basePath}?returnUrl={Uri.EscapeDataString(returnUrl)}";
    }

    /// <inheritdoc />
    public string BuildUrl(string basePath, OAuthParameters parameters, string? returnUrl)
    {
        var sb = new StringBuilder(basePath);
        sb.Append('?');
        AppendOAuthParameters(sb, parameters);

        if (!string.IsNullOrEmpty(returnUrl))
        {
            sb.Append($"&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        return sb.ToString();
    }

    private static void AppendOAuthParameters(StringBuilder sb, OAuthParameters parameters)
    {
        var first = true;

        AppendParameter(sb, "client_id", parameters.ClientId, ref first);
        AppendParameter(sb, "scope", parameters.Scope, ref first);
        AppendParameter(sb, "redirect_uri", parameters.RedirectUri, ref first);
        AppendParameter(sb, "state", parameters.State, ref first);
        AppendParameter(sb, "nonce", parameters.Nonce, ref first);
        AppendParameter(sb, "code_challenge", parameters.CodeChallenge, ref first);
        AppendParameter(sb, "code_challenge_method", parameters.CodeChallengeMethod, ref first);
        AppendParameter(sb, "response_mode", parameters.ResponseMode, ref first);
        AppendParameter(sb, "organization_id", parameters.OrganizationId, ref first);
    }

    private static void AppendParameter(StringBuilder sb, string name, string? value, ref bool first)
    {
        if (!first)
        {
            sb.Append('&');
        }

        // Always include the parameter, even if empty, for consistency
        // Uri.EscapeDataString encodes '+' as '%2B' and other special chars correctly
        sb.Append(name);
        sb.Append('=');
        sb.Append(Uri.EscapeDataString(value ?? ""));

        first = false;
    }
}
