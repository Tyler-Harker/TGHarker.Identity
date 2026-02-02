using System.Text;
using System.Web;

namespace TGharker.Identity.Web.Services;

/// <summary>
/// Builds OAuth2/OIDC responses (redirects, form_post, errors).
/// Thread-safe and can be registered as a singleton.
/// </summary>
public sealed class OAuthResponseBuilder : IOAuthResponseBuilder
{
    /// <inheritdoc />
    public IResult CreateSuccessRedirect(string redirectUri, string code, string? state, string responseMode)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["code"] = code,
            ["state"] = state
        };

        return CreateSuccessRedirect(redirectUri, parameters, responseMode);
    }

    /// <inheritdoc />
    public IResult CreateSuccessRedirect(string redirectUri, Dictionary<string, string?> parameters, string responseMode)
    {
        if (responseMode == "form_post")
        {
            return CreateFormPostResponse(redirectUri, parameters);
        }

        var queryParams = BuildQueryString(parameters);

        var finalUri = responseMode switch
        {
            "fragment" => $"{redirectUri}#{queryParams}",
            _ => redirectUri.Contains('?') ? $"{redirectUri}&{queryParams}" : $"{redirectUri}?{queryParams}"
        };

        return Results.Redirect(finalUri);
    }

    /// <inheritdoc />
    public IResult CreateErrorRedirect(string? redirectUri, string? state, string responseMode, string error, string? description)
    {
        if (string.IsNullOrEmpty(redirectUri))
        {
            return Results.BadRequest(new { error, error_description = description });
        }

        var errorParams = new Dictionary<string, string?>
        {
            ["error"] = error,
            ["error_description"] = description,
            ["state"] = state
        };

        return CreateSuccessRedirect(redirectUri, errorParams, responseMode);
    }

    /// <inheritdoc />
    public IResult CreateFormPostResponse(string redirectUri, Dictionary<string, string?> parameters)
    {
        var hiddenFields = new StringBuilder();
        foreach (var param in parameters.Where(p => p.Value != null))
        {
            var encodedValue = HttpUtility.HtmlEncode(param.Value);
            hiddenFields.AppendLine($"<input type=\"hidden\" name=\"{param.Key}\" value=\"{encodedValue}\" />");
        }

        var html = $"""
            <!DOCTYPE html>
            <html>
            <head>
                <title>Submitting...</title>
            </head>
            <body onload="document.forms[0].submit()">
                <noscript>
                    <p>JavaScript is required. Click the button below to continue.</p>
                </noscript>
                <form method="post" action="{HttpUtility.HtmlEncode(redirectUri)}">
                    {hiddenFields}
                    <noscript>
                        <button type="submit">Continue</button>
                    </noscript>
                </form>
            </body>
            </html>
            """;

        return Results.Content(html, "text/html");
    }

    private static string BuildQueryString(Dictionary<string, string?> parameters)
    {
        return string.Join("&",
            parameters.Where(p => p.Value != null).Select(p => $"{p.Key}={HttpUtility.UrlEncode(p.Value)}"));
    }
}
